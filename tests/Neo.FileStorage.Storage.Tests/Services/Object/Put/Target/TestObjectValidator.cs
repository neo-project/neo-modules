using Neo.FileStorage.Storage.Core.Object;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Put
{
    public class TestObjectValidator : IObjectValidator
    {
        public VerifyResult ValidateResult = VerifyResult.Success;
        public bool ContentResult = true;

        public VerifyResult Validate(FSObject obj)
        {
            return ValidateResult;
        }

        public bool ValidateContent(FSObject obj)
        {
            return ContentResult;
        }
    }
}
