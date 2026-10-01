using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// The MCP endpoint: JSON-RPC 2.0 over HTTP POST (the "streamable HTTP" transport, answering every
    /// request with a plain JSON body), on the loopback interface only. Requests are read on a background
    /// thread; tool calls are handed to <see cref="MainThread"/> and the thread waits for their result.
    /// </summary>
    internal sealed class McpHttpServer
    {
        private const string ServerName = "KSP-MCPServer";
        private const string ServerVersion = "0.1.0";
        private const string DefaultProtocolVersion = "2025-06-18";

        private readonly HttpListener _listener = new HttpListener();
        private readonly Dictionary<string, Tool> _tools;
        private readonly int _port;
        private Thread _thread;
        private volatile bool _running;

        public McpHttpServer(int port, IEnumerable<Tool> tools)
        {
            _port = port;
            _tools = tools.ToDictionary(t => t.Name);
        }

        /// <summary>Starts listening on http://127.0.0.1:port/mcp/. Throws when the port cannot be bound.</summary>
        public void Start()
        {
            _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/mcp/");
            _listener.Start();
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = ServerName };
            _thread.Start();
            Log.Info("Listening on http://127.0.0.1:" + _port + "/mcp/ with " + _tools.Count + " tools");
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
                // One request at a time is enough: tool calls run one after the other on the main thread
                // anyway.
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

            object parsed;
            try
            {
                parsed = Json.Parse(body);
            }
            catch (FormatException e)
            {
                Respond(response, 400, Error(null, -32700, "Parse error: " + e.Message));
                return;
            }

            Dictionary<string, object> message = parsed as Dictionary<string, object>;
            if (message == null)
            {
                Respond(response, 400, Error(null, -32600, "Invalid request: batches are not supported"));
                return;
            }

            // A notification has no id and gets no answer.
            if (!message.ContainsKey("id"))
            {
                response.StatusCode = 202;
                response.Close();
                return;
            }

            object id = message["id"];
            string method = message.ContainsKey("method") ? message["method"] as string : null;
            Dictionary<string, object> parameters = message.ContainsKey("params")
                ? message["params"] as Dictionary<string, object>
                : null;

            object answer;
            switch (method)
            {
                case "initialize":
                    answer = Result(id, Initialize(parameters));
                    break;
                case "ping":
                    answer = Result(id, new Dictionary<string, object>());
                    break;
                case "tools/list":
                    answer = Result(id, new Dictionary<string, object> { { "tools", ListTools() } });
                    break;
                case "tools/call":
                    answer = CallTool(id, parameters);
                    break;
                default:
                    answer = Error(id, -32601, "Method not found: " + method);
                    break;
            }
            Respond(response, 200, answer);
        }

        private static Dictionary<string, object> Initialize(Dictionary<string, object> parameters)
        {
            string version = parameters != null && parameters.ContainsKey("protocolVersion")
                ? parameters["protocolVersion"] as string ?? DefaultProtocolVersion
                : DefaultProtocolVersion;
            return new Dictionary<string, object>
            {
                { "protocolVersion", version },
                { "capabilities", new Dictionary<string, object> { { "tools", new Dictionary<string, object>() } } },
                { "serverInfo", new Dictionary<string, object> { { "name", ServerName }, { "version", ServerVersion } } }
            };
        }

        private List<object> ListTools()
        {
            return _tools.Values.Select(t => (object)new Dictionary<string, object>
            {
                { "name", t.Name },
                { "description", t.Description },
                { "inputSchema", t.InputSchema }
            }).ToList();
        }

        private object CallTool(object id, Dictionary<string, object> parameters)
        {
            string name = parameters != null && parameters.ContainsKey("name") ? parameters["name"] as string : null;
            Tool tool;
            if (name == null || !_tools.TryGetValue(name, out tool))
            {
                return Error(id, -32602, "Unknown tool: " + name);
            }
            Dictionary<string, object> arguments = parameters.ContainsKey("arguments")
                ? parameters["arguments"] as Dictionary<string, object>
                : null;

            ToolCall call = new ToolCall(arguments);
            Exception failure = MainThread.RunAndWait(tool.Run(call));
            if (failure != null)
            {
                call.Fail(name + " threw: " + failure.Message);
                Log.Error(name + " threw: " + failure);
            }
            if (call.Content.Count == 0)
            {
                call.Text("");
            }
            return Result(id, new Dictionary<string, object>
            {
                { "content", call.Content },
                { "isError", call.IsError }
            });
        }

        private static Dictionary<string, object> Result(object id, object result)
        {
            return new Dictionary<string, object> { { "jsonrpc", "2.0" }, { "id", id }, { "result", result } };
        }

        private static Dictionary<string, object> Error(object id, int code, string message)
        {
            return new Dictionary<string, object>
            {
                { "jsonrpc", "2.0" },
                { "id", id },
                { "error", new Dictionary<string, object> { { "code", code }, { "message", message } } }
            };
        }

        private static void Respond(HttpListenerResponse response, int status, object body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Json.Write(body));
            response.StatusCode = status;
            response.ContentType = "application/json";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }
    }
}
