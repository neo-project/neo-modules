using Neo.FileStorage.API.Object;
using System;

namespace Neo.FileStorage.Services.Object.Get.Execute
{
    public partial class ExecuteContext
    {
        private bool ProcessNode(Network.Address address)
        {
            var iport = address.IPAddressString();
            var client = GetService.ClientCache.Get(iport);
            try
            {
                collectedObject = client.GetObject(this);
                WriteCollectedObject();
                return true;
            }
            catch (Exception e) when (e is not SplitInfoException) //TODO: || is not already removed
            {
                return false;
            }
        }
    }
}
