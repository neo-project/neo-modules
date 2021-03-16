using Neo.FileStorage.API.Acl;
using static Neo.FileStorage.API.Acl.EACLRecord.Types;
using Neo.FileStorage.API.Object;
using V2Object = Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.LocalObjectStorage.LocalStore;
using Neo.FileStorage.Services.Object.Acl.EAcl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Services.Object.Acl
{
    public class HeaderSource : ITypedHeaderSource
    {
        private readonly Storage localStorage;
        private readonly object message;

        public HeaderSource(Storage local_storage, object message)
        {
            if (local_storage is null)
                throw new ArgumentNullException(nameof(local_storage));
            if (message is null)
                throw new ArgumentNullException(nameof(message));
            localStorage = local_storage;
            this.message = message;
        }

        public List<XHeader> HeadersOfSource(HeaderType type)
        {
            switch (type)
            {
                case HeaderType.Request:
                    {
                        if (message is IRequest req)
                            return req.MetaHeader?.XHeaders?.ToList();
                        return null;
                    }
                case HeaderType.Object:
                    return ObjectHeaders();
                default:
                    return null;
            };
        }

        private List<XHeader> ObjectHeaders()
        {
            switch (message)
            {
                case IRequest request:
                    switch (request)
                    {
                        case GetRequest getRequest:
                            return LocalObjectHeaders(getRequest.Body.Address);
                        case DeleteRequest deleteRequest:
                            return LocalObjectHeaders(deleteRequest.Body.Address);
                        case HeadRequest headRequest:
                            return LocalObjectHeaders(headRequest.Body.Address);
                        case GetRangeRequest getRangeRequest:
                            return LocalObjectHeaders(getRangeRequest.Body.Address);
                        case GetRangeHashRequest getRangeHashRequest:
                            return LocalObjectHeaders(getRangeHashRequest.Body.Address);
                        case PutRequest putRequest:
                            if (putRequest.Body.ObjectPartCase == PutRequest.Types.Body.ObjectPartOneofCase.Init)
                            {
                                var init = putRequest.Body.Init;
                                var obj = new V2Object.Object
                                {
                                    ObjectId = init.ObjectId,
                                    Header = init.Header,
                                };
                                return HeadersFromObject(obj);
                            }
                            break;
                        default:
                            throw new InvalidOperationException(nameof(ObjectHeaders));
                    }
                    break;
                case IResponse response:
                    switch (response)
                    {
                        case GetResponse getResponse:
                            if (getResponse.Body.ObjectPartCase == GetResponse.Types.Body.ObjectPartOneofCase.Init)
                            {
                                var init = getResponse.Body.Init;
                                var obj = new V2Object.Object
                                {
                                    ObjectId = init.ObjectId,
                                    Header = init.Header,
                                };
                                return HeadersFromObject(obj);
                            }
                            break;
                        case HeadResponse headResponse:
                            {
                                var obj = new V2Object.Object();
                                var hdr = new Header();
                                switch (headResponse.Body.HeadCase)
                                {
                                    case HeadResponse.Types.Body.HeadOneofCase.ShortHeader:
                                        {
                                            var shortHeader = headResponse.Body.ShortHeader;
                                            hdr.Version = shortHeader.Version;
                                            hdr.CreationEpoch = shortHeader.CreationEpoch;
                                            hdr.OwnerId = shortHeader.OwnerId;
                                            hdr.ObjectType = shortHeader.ObjectType;
                                            hdr.PayloadLength = shortHeader.PayloadLength;
                                            break;
                                        }
                                    case HeadResponse.Types.Body.HeadOneofCase.Header:
                                        {
                                            hdr = headResponse.Body.Header.Header;
                                            break;
                                        }
                                    default:
                                        throw new InvalidOperationException(nameof(ObjectHeaders));
                                }
                                obj.Header = hdr;
                                return HeadersFromObject(obj);
                            }
                        default:
                            break;
                    }
                    break;
                default:
                    throw new InvalidOperationException(nameof(ObjectHeaders));
            }
            return new List<XHeader>();
        }

        private List<XHeader> LocalObjectHeaders(Address address)
        {
            var obj = localStorage.Get(address);
            if (obj is null) return new List<XHeader>();
            return HeadersFromObject(obj);
        }

        private List<XHeader> HeadersFromObject(V2Object.Object obj)
        {
            var headers = new List<XHeader>();
            while (obj != null)
            {
                headers.Add(new XHeader
                {
                    Key = Filter.FilterObjectContainerID,
                    Value = obj.Header.ContainerId.ToBase58String(),
                });
                headers.Add(new XHeader
                {
                    Key = Filter.FilterObjectOwnerID,
                    Value = obj.Header.OwnerId.ToBase58String(),
                });
                headers.Add(new XHeader
                {
                    Key = Filter.FilterObjectCreationEpoch,
                    Value = obj.Header.CreationEpoch.ToString(),
                });
                headers.Add(new XHeader
                {
                    Key = Filter.FilterObjectPayloadLength,
                    Value = obj.Header.PayloadLength.ToString(),
                });
                headers.Add(new XHeader
                {
                    Key = Filter.FilterObjectID,
                    Value = obj.ObjectId.ToString(),
                });
                foreach (var attr in obj.Header.Attributes)
                {
                    headers.Add(new XHeader
                    {
                        Key = attr.Key,
                        Value = attr.Value,
                    });
                }
                obj = obj.Parent;
            }
            return headers;
        }
    }
}