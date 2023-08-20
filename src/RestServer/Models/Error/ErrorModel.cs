namespace Neo.Plugins.RestServer.Models.Error
{
    internal abstract class ErrorModel
    {
        public int Code { get; init; } = -1;
        public string Name { get; init; } = "UnknownError";
        public string Message { get; init; } = "An error occurred.";
    }
}
