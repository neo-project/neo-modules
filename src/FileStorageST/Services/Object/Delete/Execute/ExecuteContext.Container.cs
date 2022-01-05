using static Neo.Utility;

namespace Neo.FileStorage.Storage.Services.Object.Delete.Execute
{
    public partial class ExecuteContext
    {
        private void ExecuteOnContainer()
        {
            Log("DeleteExecutor", LogLevel.Warning, "request is not rolled over to the container");
        }
    }
}
