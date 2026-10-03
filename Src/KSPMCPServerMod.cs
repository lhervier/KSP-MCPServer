using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Starts the MCP server when KSP starts and keeps it for the whole session, across scene changes.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class KSPMCPServerMod : MonoBehaviour
    {
        private const int DefaultPort = 8770;

        private McpHttpServer _server;
        private bool _subscribed;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            MainThread.SetHost(this);

            List<Tool> tools = new List<Tool>();
            tools.AddRange(GameTools.All());
            tools.AddRange(DriveTools.All());
            tools.AddRange(FlightTools.All());
            tools.AddRange(ReflectionTools.All());
            tools.AddRange(ExtensionTools.All());

            try
            {
                _server = new McpHttpServer(ReadPort(), tools);
                _server.Start();
            }
            catch (Exception e)
            {
                Log.Error("Could not start: " + e.Message);
                _server = null;
            }
        }

        private void Update()
        {
            Subscribe();
            MainThread.Pump();
        }

        private void OnDestroy()
        {
            _server?.Stop();
            if (_subscribed)
            {
                GameEvents.onFloatingOriginShift.Remove(OnOriginShift);
                GameEvents.onFlightReady.Remove(OnFlightReady);
            }
        }

        // The game events, subscribed once they exist: KSP creates them after the very first addons start.
        // Instance methods: EventData refuses a static handler.
        private void Subscribe()
        {
            if (_subscribed || GameEvents.onFloatingOriginShift == null || GameEvents.onFlightReady == null)
            {
                return;
            }
            GameEvents.onFloatingOriginShift.Add(OnOriginShift);
            GameEvents.onFlightReady.Add(OnFlightReady);
            _subscribed = true;
        }

        private void OnOriginShift(Vector3d offset, Vector3d nonFrame)
        {
            GameTools.OnOriginShift(offset, nonFrame);
        }

        private void OnFlightReady()
        {
            GameTools.OnFlightReady();
        }

        // The port, from PluginData/settings.cfg next to the DLL; the default when it is not there.
        private static int ReadPort()
        {
            string path = Path.Combine(Path.GetDirectoryName(typeof(KSPMCPServerMod).Assembly.Location) ?? "", "PluginData/settings.cfg");
            ConfigNode node = File.Exists(path) ? ConfigNode.Load(path) : null;
            int port;
            if (node != null && int.TryParse(node.GetValue("port"), out port))
            {
                return port;
            }
            return DefaultPort;
        }
    }
}
