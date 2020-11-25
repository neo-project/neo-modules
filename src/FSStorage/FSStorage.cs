
namespace Neo.Plugins
{
    public class FSStorage : Plugin, IPersistencePlugin
    {
        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }
    }
}