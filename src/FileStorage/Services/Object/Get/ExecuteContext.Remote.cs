using Neo.FileStorage.API.Client.ObjectParams;
using Neo.FileStorage.API.Object.Exceptions;
using System;
using System.Threading;

namespace Neo.FileStorage.Services.Object.Get
{
    public partial class ExecuteContext
    {
        private bool ProcessNode(Network.Address address)
        {
            var iport = address.IPAddressString();
            var client = GetService.ClientCache.Get(iport);
            try
            {
                collectedObject = client.GetObject(new GetObjectParams { Address = Prm.Address, Raw = Prm.Raw }, context: new CancellationTokenSource().Token).Result;
                WriteCollectedObject();
                return true;
            }
            catch (Exception e) when (e is not SplitInfoException) //TODO: already removed
            {
                return false;
            }
        }
    }
}
