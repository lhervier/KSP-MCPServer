using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// The MCP endpoint: JSON-RPC 2.0 over HTTP POST (the "streamable HTTP" transport, answering every
    /// request with a plain JSON body), on the loopback interface only. Each request is read and answered on
    /// a thread of its own, so that a long tool does not hold the others up; tool calls are handed to
    /// <see cref="MainThread"/> and the thread waits for their result.
    /// </summary>
    internal sealed class McpHttpServer
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly McpProtocol _protocol;
        private readonly int _port;
        private Thread _thread;
        private volatile bool _running;

        public McpHttpServer(int port, McpProtocol protocol)
        {
            _port = port;
            _protocol = protocol;
        }

        /// <summary>Starts listening on http://127.0.0.1:port/mcp/. Throws when the port cannot be bound.</summary>
        public void Start()
        {
            _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/mcp/");
            _listener.Start();
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = McpProtocol.ServerName };
            _thread.Start();
            Log.Info("Listening on http://127.0.0.1:" + _port + "/mcp/ with " + _protocol.ToolCount + " tools");
        }

        public void Stop()
        {
            _running = false;
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception)
            {
                // Closing a listener that is already gone is not worth reporting.
            }
        }

        private void Loop()
        {
            while (_running)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (Exception)
                {
                    // Stop() makes GetContext throw: that is how the loop ends.
                    return;
                }
                new Thread(() => Serve(context)) { IsBackground = true, Name = McpProtocol.ServerName + " request" }.Start();
            }
        }

        // Answers one request, a server error when anything goes wrong.
        private void Serve(HttpListenerContext context)
        {
            try
            {
                Handle(context);
            }
            catch (Exception e)
            {
                Log.Error("Request failed: " + e);
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch (Exception)
                {
                    // The client may already be gone.
                }
            }
        }

        private void Handle(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            if (request.HttpMethod != "POST")
            {
                // No server-initiated stream: GET is not offered, as the transport allows.
                response.StatusCode = 405;
                response.Close();
                return;
            }

            string body;
            using (StreamReader reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }
            int status;
            object answer = _protocol.Handle(body, out status);
            if (answer == null)
            {
                response.StatusCode = status;
                response.Close();
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(Json.Write(answer));
            response.StatusCode = status;
            response.ContentType = "application/json";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }
    }
}
