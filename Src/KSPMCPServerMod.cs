using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Starts the MCP server when KSP starts and keeps it for the whole session, across scene changes.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class KSPMCPServerMod : MonoBehaviour
    {
        private McpHttpServer _server;
        private bool _subscribed;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            MainThread.SetHost(this);

            List<Tool> tools = new List<Tool>();
            tools.AddRange(StateTools.All());
            tools.AddRange(GameTools.All());
            tools.AddRange(SceneTools.All());
            tools.AddRange(VesselTools.All());
            tools.AddRange(SpaceCenterTools.All());
            tools.AddRange(TimeTools.All());
            tools.AddRange(ViewTools.All());
            tools.AddRange(FlightTools.All());
            tools.AddRange(DriveTools.All());
            tools.AddRange(ReflectionTools.All());
            tools.AddRange(ExtensionTools.All());

            Settings settings = Settings.Read();
            ToolMessage.SetEnabled(settings.ScreenMessages);
            try
            {
                _server = new McpHttpServer(settings.Port, new McpProtocol(tools));
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
            GameState.OnOriginShift(offset, nonFrame);
        }

        private void OnFlightReady()
        {
            GameState.OnFlightReady();
        }
    }
}
