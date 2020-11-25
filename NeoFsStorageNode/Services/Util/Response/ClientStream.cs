namespace Neo.Fs.Services.Util.Response
{
    // ClientMessageStreamer represents client-side message streamer
    // that sets meta values to the response.
    public class ClientMessageStreamer
    {
        private Cfg cfg;
        private RequestMessageWriter send;
        private ClientStreamCloser close;

        public ClientMessageStreamer(Cfg c, RequestMessageWriter sender, ClientStreamCloser closer)
        {
            this.cfg = c;
            this.send = sender;
            this.close = closer;
        }

        public void Send(object req)
        {
            this.send(req);
        }

        public IResponseMessage CloseAndRecv()
        {
            var resp = this.close();
            Helper.SetMeta(resp, this.cfg);
            return resp;
        }
    }
}
