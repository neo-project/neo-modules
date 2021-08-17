namespace Neo.FileStorage.Storage.Core.Object
{
    public enum VerifyResult : byte
    {
        Success = 0,
        Null,
        NoID,
        NoHeader,
        NoContainerID,
        DuplicateAttribute,
        EmptyAttributeValue,
        InvalidKey,
        Expiration,
        InvalidSignature
    }
}
