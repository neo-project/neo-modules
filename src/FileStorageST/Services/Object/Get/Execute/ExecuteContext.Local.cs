namespace Neo.FileStorage.Storage.Services.Object.Get.Execute
{
    public partial class ExecuteContext
    {
        private void ExecuteLocal()
        {
            collectedObject = GetService.LocalStorage.GetObject(this);
            WriteCollectedObject();
        }
    }
}
