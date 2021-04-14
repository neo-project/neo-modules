using Neo.FileStorage.Morph.Event;

namespace Neo.FileStorage.InnerRing.Processors
{
    public interface IProcessor
    {
        ParserInfo[] ListenerParsers();
        HandlerInfo[] ListenerHandlers();
        HandlerInfo[] TimersHandlers();
    }
}
