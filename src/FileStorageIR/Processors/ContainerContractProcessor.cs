using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Akka.Actor;
using Google.Protobuf;
using Neo.Cryptography;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Listen;
using Neo.FileStorage.Listen.Event;
using Neo.FileStorage.Listen.Event.Morph;
using static Neo.FileStorage.Utils.WorkerPool;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class ContainerContractProcessor : BaseProcessor
    {
        public override string Name => "ContainerContractProcessor";
        private const string PutNotification = "containerPut";
        private const string DeleteNotification = "containerDelete";
        private const string SetEACLNotification = "setEACL";

        public override HandlerInfo[] ListenerHandlers()
        {
            HandlerInfo putHandler = new();
            putHandler.ScriptHashWithType = new ScriptHashWithType() { Type = PutNotification, ScriptHashValue = ContainerContractHash };
            putHandler.Handler = HandlePut;
            HandlerInfo deleteHandler = new();
            deleteHandler.ScriptHashWithType = new ScriptHashWithType() { Type = DeleteNotification, ScriptHashValue = ContainerContractHash };
            deleteHandler.Handler = HandleDelete;
            HandlerInfo setEACLHandler = new();
            setEACLHandler.ScriptHashWithType = new ScriptHashWithType() { Type = SetEACLNotification, ScriptHashValue = ContainerContractHash };
            setEACLHandler.Handler = HandleSetEACL;
            return new HandlerInfo[] { putHandler, deleteHandler, setEACLHandler };
        }

        public override ParserInfo[] ListenerParsers()
        {
            ParserInfo putParser = new();
            putParser.ScriptHashWithType = new ScriptHashWithType() { Type = PutNotification, ScriptHashValue = ContainerContractHash };
            putParser.Parser = ContainerPutEvent.ParseContainerPutEvent;
            ParserInfo deleteParser = new();
            deleteParser.ScriptHashWithType = new ScriptHashWithType() { Type = DeleteNotification, ScriptHashValue = ContainerContractHash };
            deleteParser.Parser = ContainerDeleteEvent.ParseContainerDeleteEvent;
            ParserInfo setEACLParser = new();
            setEACLParser.ScriptHashWithType = new ScriptHashWithType() { Type = SetEACLNotification, ScriptHashValue = ContainerContractHash };
            setEACLParser.Parser = ContainerSetEACLEvent.ParseContainerSetEACLEvent;
            return new ParserInfo[] { putParser, deleteParser, setEACLParser };
        }

        public void HandlePut(ContractEvent morphEvent)
        {
            ContainerPutEvent putEvent = (ContainerPutEvent)morphEvent;
            var id = putEvent.RawContainer.Sha256();
            Utility.Log(Name, LogLevel.Info, $"event, type:container put, id={Base58.Encode(id)}");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessContainerPut(putEvent)) });
        }

        public void HandleDelete(ContractEvent morphEvent)
        {
            ContainerDeleteEvent deleteEvent = (ContainerDeleteEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, $"event, type=container_delete, id={Base58.Encode(deleteEvent.ContainerID)}");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessContainerDelete(deleteEvent)) });
        }

        public void HandleSetEACL(ContractEvent morphEvent)
        {
            ContainerSetEACLEvent setEACLEvent = (ContainerSetEACLEvent)morphEvent;
            Utility.Log(Name, LogLevel.Info, "event, type=setEACL");
            WorkPool.Tell(new NewTask() { Process = Name, Task = new Task(() => ProcessContainerSetEACL(setEACLEvent)) });
        }

        public void ProcessContainerPut(ContainerPutEvent putEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore container put");
                return;
            }
            var cnrData = putEvent.RawContainer;
            Container container = null;
            try
            {
                container = Container.Parser.ParseFrom(cnrData);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"could not unmarshal container structure, error={e}");
                return;
            }
            if (!putEvent.PublicKey.LoadPublicKey().VerifyData(cnrData, putEvent.Signature, HashAlgorithmName.SHA256))
            {
                Utility.Log(Name, LogLevel.Error, "invalid signature");
                return;
            }
            try
            {
                CheckFormat(container);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"container with incorrect format detected, error={e}");
            }
            SessionToken token = null;
            if (putEvent.token != null && putEvent.token.Any())
            {
                try
                {
                    token = SessionToken.Parser.ParseFrom(putEvent.token);
                    CheckTokenContext(token, (ContainerSessionContext c) => { return c.Verb == ContainerSessionContext.Types.Verb.Put; });
                }
                catch (Exception e)
                {
                    Utility.Log(Name, LogLevel.Error, $"container checkToken context fault, error={e}");
                    return;
                }
            }
            ContainerWithSignature cnr = new()
            {
                Container = container,
                Signature = new Signature()
                {
                    Key = ByteString.CopyFrom(putEvent.PublicKey),
                    Sign = ByteString.CopyFrom(putEvent.Signature)
                },
                SessionToken = token
            };
            CheckKeyOwnership(cnr, putEvent.PublicKey);
            try
            {
                MorphInvoker.RegisterContainer(putEvent.PublicKey, putEvent.RawContainer, putEvent.Signature, putEvent.token);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't invoke new container, error={e}");
            }
        }

        public void ProcessContainerDelete(ContainerDeleteEvent deleteEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore container put");
                return;
            }
            var checkKeys = new List<byte[]>();
            try
            {
                var cnr = MorphInvoker.GetContainer(ContainerID.FromSha256Bytes(deleteEvent.ContainerID));
                SessionToken token = null;
                if (deleteEvent.token != null && deleteEvent.token.Any())
                {
                    token = SessionToken.Parser.ParseFrom(deleteEvent.token);
                    var containerId = ContainerID.FromSha256Bytes(deleteEvent.ContainerID);
                    CheckTokenContextWithCID(token, containerId, (ContainerSessionContext c) => { return c.Verb == ContainerSessionContext.Types.Verb.Delete; });
                    var key = token.Body.SessionKey.ToByteArray();
                    CheckKeyOwnershipWithToken(cnr, key, token);
                    checkKeys.Add(key);
                }
                else
                {
                    var keys = MorphInvoker.AccountKeys(cnr.Container.OwnerId.Value.ToByteArray());
                    checkKeys.AddRange(keys);
                }
                var cidHash = deleteEvent.ContainerID.Sha256();
                var sig = deleteEvent.Signature;
                if (!checkKeys.Any(p => p.LoadPublicKey().VerifyData(cidHash, sig, HashAlgorithmName.SHA256))) throw new InvalidOperationException("signature verification failed on all owner keys");
                MorphInvoker.RemoveContainer(deleteEvent.ContainerID, deleteEvent.Signature, deleteEvent.token);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, $"can't invoke delete container, error={e}");
            }
        }

        public void ProcessContainerSetEACL(ContainerSetEACLEvent setEACLEvent)
        {
            if (!State.IsAlphabet())
            {
                Utility.Log(Name, LogLevel.Info, "non alphabet mode, ignore container put");
                return;
            }
            try
            {
                var table = EACLTable.Parser.ParseFrom(setEACLEvent.Table);
                var signature = Signature.Parser.ParseFrom(setEACLEvent.Signature);
                var sessionToken = SessionToken.Parser.ParseFrom(setEACLEvent.Token);
                if (!setEACLEvent.PublicKey.LoadPublicKey().VerifyHash(table.Sha256Checksum().Sum.ToByteArray(), setEACLEvent.Signature))
                {
                    Utility.Log(Name, LogLevel.Error, "invalid signature");
                    return;
                }
                var cnr = MorphInvoker.GetContainer(table.ContainerId);
                if (sessionToken != null && setEACLEvent.Token.Any())
                {
                    CheckTokenContextWithCID(sessionToken, table.ContainerId, (ContainerSessionContext c) => { return c.Verb == ContainerSessionContext.Types.Verb.Seteacl; });
                }
                CheckKeyOwnership(cnr, setEACLEvent.PublicKey);
                MorphInvoker.SetEACL(table, signature, sessionToken);
            }
            catch (Exception e)
            {
                Utility.Log(Name, LogLevel.Error, string.Format("could not approve set EACL,error:{0}", e.Message));
            }
        }

        public void CheckFormat(Container container)
        {
            if (container.PlacementPolicy is null) throw new FormatException("placement policy is null");
            if (!API.Refs.Version.IsSupportedVersion(container.Version)) throw new FormatException("incorrect version");
            if (container.OwnerId.Value.Length != 25) throw new FormatException($"incorrect owner identifier, expected={25}, actual={container.OwnerId.Value.Length}");
            if (container.Nonce.ToByteArray().Length != 16) throw new FormatException("incorrect nonce");
        }

        public void CheckKeyOwnership(ContainerWithSignature ownerIDSource, byte[] key)
        {
            var token = ownerIDSource.SessionToken;
            if (token is not null)
            {
                CheckKeyOwnershipWithToken(ownerIDSource, key, token);
                return;
            }
            if (ownerIDSource.Container.OwnerId.Equals(key.PublicKeyToOwnerID())) return;
            var ownerKeys = MorphInvoker.AccountKeys(ownerIDSource.Container.OwnerId.Value.ToByteArray());
            if (ownerKeys is null) throw new FormatException("could not received owner keys");
            if (!ownerKeys.Any(p => p.Equals(key))) throw new FormatException($"key {key.ToHexString()} is not tied to the owner of the container");
        }

        public void CheckKeyOwnershipWithToken(ContainerWithSignature ownerIDSource, byte[] key, SessionToken token)
        {
            if (!key.SequenceEqual(token.Body.SessionKey.ToByteArray())) throw new FormatException("signed with a non-session key");
            if (!token.Body.OwnerId.Equals(ownerIDSource.Container.OwnerId)) throw new FormatException("owner differs with token owner");
            CheckSessionToken(token);
        }

        public void CheckSessionToken(SessionToken token)
        {
            if (!token.VerifySignature()) throw new FormatException("invalid signature");
            var curEpoch = State.EpochCounter();
            var nbf = token.Body.Lifetime.Nbf;
            if (curEpoch < nbf) throw new FormatException($"token is not valid yet, nbf={nbf}, current={curEpoch}");
            var iat = token.Body.Lifetime.Iat;
            if (curEpoch < iat) throw new FormatException($"token is issued in future, iat={iat}, current={curEpoch}");
            var exp = token.Body.Lifetime.Exp;
            if (curEpoch < exp) throw new FormatException($"token is expired, exp={exp}, current={curEpoch}");
        }

        public ContainerSessionContext ContextWithVerifiedVerb(SessionToken token, Func<ContainerSessionContext, bool> verbAssert)
        {
            ContainerSessionContext c = token.Body.Container;
            if (c is null) throw new FormatException("wrong session context");
            if (!verbAssert(c)) throw new FormatException("wrong token verb");
            return c;
        }

        public void CheckTokenContext(SessionToken token, Func<ContainerSessionContext, bool> verbAssert)
        {
            ContextWithVerifiedVerb(token, verbAssert);
        }

        public void CheckTokenContextWithCID(SessionToken token, ContainerID id, Func<ContainerSessionContext, bool> verbAssert)
        {
            var c = ContextWithVerifiedVerb(token, verbAssert);
            var tokCID = c.ContainerId;
            if (tokCID is not null && !tokCID.Equals(id)) throw new FormatException("wrong container ID");
        }
    }
}
