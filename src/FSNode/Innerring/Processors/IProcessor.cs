namespace Neo.Plugins.FSStorage.innerring.processors
{
    public interface IProcessor
    {
        string Name { get; set; }
        ParserInfo[] ListenerParsers();
        HandlerInfo[] ListenerHandlers();
        HandlerInfo[] TimersHandlers();
    }
}
