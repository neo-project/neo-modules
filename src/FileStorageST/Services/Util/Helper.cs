using Neo.FileStorage.API.Session;

namespace Neo.FileStorage.Storage.Services.Util
{
    public static class Helper
    {
        public static bool IsStatusSupported(IRequest request)
        {
            var version = request.MetaHeader?.Version;
            return version is not null && (version.Major > 2 || (version.Major == 2 && version.Minor > 10));
        }
    }
}
