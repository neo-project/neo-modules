using Neo.FileStorage.Morph.Event;

namespace Neo.FileStorage.Morph.Listen
{
    public interface IProcessor
    {
        ParserInfo[] ListenerParsers();
        HandlerInfo[] ListenerHandlers();
        HandlerInfo[] TimersHandlers();
    }
}
