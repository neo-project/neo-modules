namespace Neo.Plugins.FSStorage.innerring.processors
{
    public interface IProcessor
    {
        ParserInfo[] ListenerParsers();
        HandlerInfo[] ListenerHandlers();
        HandlerInfo[] TimersHandlers();
    }
}
