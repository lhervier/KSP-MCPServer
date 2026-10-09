using System.IO;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// The settings of the mod, read once from <c>PluginData/settings.cfg</c> next to the DLL; each one has
    /// its default when the file, or the setting, is not there.
    /// </summary>
    internal sealed class Settings
    {
        /// <summary>The TCP port the server listens on.</summary>
        public readonly int Port = 8770;

        /// <summary>Whether each tool shows its name at the top of the screen while it runs.</summary>
        public readonly bool ScreenMessages = true;

        private Settings(ConfigNode node)
        {
            if (node == null)
            {
                return;
            }
            int port;
            if (int.TryParse(node.GetValue("port"), out port))
            {
                Port = port;
            }
            bool screenMessages;
            if (bool.TryParse(node.GetValue("screen_messages"), out screenMessages))
            {
                ScreenMessages = screenMessages;
            }
        }

        /// <summary>Reads the settings file.</summary>
        public static Settings Read()
        {
            string path = Path.Combine(Path.GetDirectoryName(typeof(Settings).Assembly.Location) ?? "", "PluginData/settings.cfg");
            return new Settings(File.Exists(path) ? ConfigNode.Load(path) : null);
        }
    }
}
