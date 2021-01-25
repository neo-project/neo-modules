
namespace Neo.FSNode.Services.Session.Storage
{
    public class Key
    {
        private string tokenID;
        private string ownerID;

        public Key(string tokenId, string ownerId)
        {
            this.tokenID = tokenId;
            this.ownerID = ownerId;
        }
    }
}
