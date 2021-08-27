using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Core.Object
{
    public interface IObjectValidator
    {
        VerifyResult Validate(FSObject obj);
        bool ValidateContent(FSObject obj);
    }
}
