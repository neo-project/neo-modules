using Grpc.Core;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using System;

namespace Neo.FileStorage.Storage.Services.Object.Get.Execute
{
    public partial class ExecuteContext
    {
        private bool ProcessNode(NodeInfo node)
        {
            var client = GetService.ClientCache.Get(node);
            try
            {
                collectedObject = client.GetObject(this);
                WriteCollectedObject();
                return true;
            }
            catch (Exception e)
            {
                if (e is AggregateException ae)
                {
                    foreach (var ie in ae.InnerExceptions)
                    {
                        if (ie is SplitInfoException se)
                        {
                            throw new SplitInfoException(se.SplitInfo);
                        }
                        if (ie is RpcException re)
                        {
                            if (re.StatusCode == StatusCode.Unknown)
                            {
                                switch (re.Status.Detail)
                                {
                                    case ObjectException.AlreadyRemovedError:
                                        throw new ObjectAlreadyRemovedException();
                                    case ObjectException.RangeOutOfBoundsError:
                                        throw new RangeOutOfBoundsException();
                                    case ObjectException.NotFoundError:
                                        lastException = new ObjectNotFoundException();
                                        return false;
                                }
                            }
                            lastException = new Exception(re.Status.Detail);
                            continue;
                        }
                        lastException = ie;
                    }
                }
                else
                    lastException = e;
                return false;
            }
        }
    }
}
