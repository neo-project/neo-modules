using NeoFS.API.v2.Session;

namespace Neo.FSNode.Services.Util
{
    //public interface IResponseMessage
    //{
    //    ResponseMetaHeader GetMetaHeader();
    //    void SetMetaHeader(ResponseMetaHeader header);
    //}

    public delegate IResponse UnaryHandler(object obj);

    public class SignService
    {
        private byte[] key; // private key

        public SignService(byte[] pk)
        {
            this.key = pk;
        }

        public RequestMessageStreamer CreateRequestStreamer(RequestMessageWriter sender, ClientStreamCloser closer)
        {
            return new RequestMessageStreamer(this.key, sender, closer);
        }

        public ResponseMessageStreamer HandleServerStreamRequest(object req, ServerStreamHandler handler)
        {
            // signature.VerifyServiceMessage(req)
            //Signature
            var msgRdr = handler(req);
            return new ResponseMessageStreamer(this.key, msgRdr);
        }

        public IResponse HandleUnaryRequest(object req, UnaryHandler handler)
        {
            // signature.VerifyServiceMessage(req)
            var resp = handler(req);
            // signature.SignServiceMessage(this.key, resp);
            return resp;
        }
    }

    public delegate IResponse ResponseMessageReader();

    public delegate ResponseMessageReader ServerStreamHandler(object obj);

    public class ResponseMessageStreamer
    {
        private byte[] key; // private key
        private ResponseMessageReader recv;

        public ResponseMessageStreamer(byte[] pk, ResponseMessageReader reader)
        {
            this.key = pk;
            this.recv = reader;
        }

        public IResponse Recv()
        {
            var m = this.recv();
            // signature.SignServiceMessage(this.key, m)
            return m;
        }
    }

    public delegate void RequestMessageWriter(object obj);

    public delegate IResponse ClientStreamCloser();

    public class RequestMessageStreamer
    {
        private byte[] key; // private key
        private RequestMessageWriter send;
        private ClientStreamCloser close;

        public RequestMessageStreamer(byte[] pk, RequestMessageWriter sender, ClientStreamCloser closer)
        {
            this.key = pk;
            this.send = sender;
            this.close = closer;
        }

        public void Send(object req)
        {
            //Signature.Parser.ParseFrom(req); // signature.VerifyServiceMessage
            this.send(req);
        }

        public IResponse CloseAndRecv()
        {
            var resp = this.close();
            // signature.SignServiceMessage(s.key, resp)
            return resp;
        }
    }
}
