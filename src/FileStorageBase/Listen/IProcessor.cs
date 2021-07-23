namespace Neo.FileStorage.Listen
{
    public interface IProcessor
    {
        ParserInfo[] ListenerParsers();
        HandlerInfo[] ListenerHandlers();
        HandlerInfo[] TimersHandlers();
    }
}
