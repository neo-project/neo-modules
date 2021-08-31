using System;
using System.Collections.Generic;
using Grpc.Core;
using Neo.FileStorage.API.Object;

namespace Neo.FileStorage.Storage.Services.Object.Get.Execute
{
    public partial class ExecuteContext
    {
        private bool ProcessNode(List<Network.Address> address)
        {
            var client = GetService.ClientCache.Get(address);
            try
            {
                collectedObject = client.GetObject(this);
                WriteCollectedObject();
                return true;
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    if (e is SplitInfoException se)
                    {
                        throw new SplitInfoException(se.SplitInfo);
                    }
                    if (e is RpcException re &&
                        re.StatusCode == StatusCode.Unknown &&
                        re.Status.Detail == ObjectException.AlreadyRemovedError)
                    {
                        throw new ObjectAlreadyRemovedException();
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
