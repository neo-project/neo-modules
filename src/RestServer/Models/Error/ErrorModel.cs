namespace Neo.Plugins.RestServer.Models.Error
{
    internal class ErrorModel
    {
        public int Code { get; init; } = -1;
        public string Name { get; init; } = "UnknownError";
        public string Message { get; init; } = "UnknownError";

        public static ErrorModel Create(Exception exception) =>
            new()
            {
                Code = exception?.HResult ?? 100,
                Name = nameof(exception),
                Message = exception?.Message ?? "An error occurred.",
            };
    }
}
