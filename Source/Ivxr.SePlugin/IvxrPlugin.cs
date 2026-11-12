using System.IO;
using Iv4xr.PluginLib;
using Iv4xr.PluginLib.Json;
using Iv4xr.SePlugin.Config;
using VRage.FileSystem;
using VRage.Plugins;

namespace Iv4xr.SePlugin
{
    public class IvxrPlugin : IConfigurablePlugin, IHandleInputPlugin
    {
        public static IvxrPluginContext Context { get; private set; }

        // Shortcuts
        public static ILog Log { get; private set; }

        public void Init(object gameInstance)
        {
            if (Context != null)
            {
                Log.WriteLine("Init already called.");
                return;
            }
            // TG: Plugin bleibt ganz aus (keine Schnittstelle, kein Eingriff in Eingabe und Takt),
            // solange die Schalterdatei fehlt. Gelesen nur beim Spielstart.
            var schalter = Path.Combine(MyFileSystem.UserDataPath, "ivxr-an.txt");
            if (!File.Exists(schalter))
            {
                VRage.Utils.MyLog.Default.WriteLineAndConsole($"TG iv4XR: {schalter} fehlt, Plugin bleibt aus.");
                return;
            }

            var config = GetConfiguration(MyFileSystem.UserDataPath) as IvxrPluginConfiguration;

            Context = new IvxrPluginContext(config.ToPluginConfig());

            Log = Context.Log;
            Log.WriteLine($"{nameof(IvxrPlugin)} initialization finished.");
            VRage.Utils.MyLog.Default.WriteLineAndConsole("TG iv4XR: Schnittstelle startet auf 127.0.0.1.");

            Context.StartServer();
        }

        public void Update()
        {
            Context?.Update();
        }

        public void HandleInput()
        {
            Context?.HandleInput();
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!alreadyDisposed)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects).
                    Context?.Dispose();
                }

                // TODO: Set large fields to null.

                alreadyDisposed = true;
            }
        }

        #region The rest of IDisposable Support

        private bool alreadyDisposed = false; // To detect redundant calls

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~PluginMain() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion

        public string GetPluginTitle()
        {
            return "IV4XR";
        }

        public IPluginConfiguration GetConfiguration(string userDataPath)
        {
            var configPath = Path.Combine(userDataPath, ConfigLoader.CONFIG_FILE);
            var config = new ConfigLoader(Log, new NewtonJsoner(), configPath).LoadOrSaveDefault();
            return new IvxrPluginConfiguration(config, Log);
        }
    }
}
