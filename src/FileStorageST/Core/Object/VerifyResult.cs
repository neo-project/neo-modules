namespace Neo.FileStorage.Storage.Core.Object
{
    public enum VerifyResult : byte
    {
        Success = 0,
        Null,
        InvalidID,
        NoHeader,
        InvalidContainerID,
        InvalidOwnerID,
        DuplicateAttribute,
        EmptyAttributeValue,
        InvalidKey,
        Expiration,
        InvalidSignature
    }
}
