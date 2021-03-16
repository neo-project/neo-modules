using Neo.FileStorage.Morph.Event;

namespace Neo.FileStorage.InnerRing.Processors
{
    public interface IProcessor
    {
        string Name { get; set; }
        ParserInfo[] ListenerParsers();
        HandlerInfo[] ListenerHandlers();
        HandlerInfo[] TimersHandlers();
    }
}
