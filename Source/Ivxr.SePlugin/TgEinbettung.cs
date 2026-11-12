using System;
using System.IO;
using System.Reflection;

namespace System.Runtime.CompilerServices
{
    // Fuer [ModuleInitializer] unter net48 (der Compiler braucht nur das Attribut)
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}

namespace Iv4xr.SePlugin
{
    /// <summary>
    /// TG: Pulsar laedt lokale Plugins als einzelne DLL. Die Abhaengigkeiten (Ivxr.SpaceEngineers,
    /// Ivxr.PlugIndependentLib, AustinHarris.JsonRpc, ImpromptuInterface, Newtonsoft.Json) liegen
    /// als Ressourcen "tg.&lt;Name&gt;.dll" in dieser DLL und werden hier bei Bedarf geladen.
    /// Ist eine Bibliothek gleichen Namens schon geladen (z. B. Newtonsoft.Json von Pulsar),
    /// wird diese genommen.
    /// </summary>
    internal static class TgEinbettung
    {
        private static readonly object Sperre = new object();

        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void Start()
        {
            AppDomain.CurrentDomain.AssemblyResolve += Aufloesen;
        }

        private static Assembly Aufloesen(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            lock (Sperre)
            {
                foreach (var geladen in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (geladen.GetName().Name == name)
                    {
                        return geladen;
                    }
                }

                using (var strom = typeof(TgEinbettung).Assembly.GetManifestResourceStream("tg." + name + ".dll"))
                {
                    if (strom == null)
                    {
                        return null;
                    }

                    using (var speicher = new MemoryStream())
                    {
                        strom.CopyTo(speicher);
                        return Assembly.Load(speicher.ToArray());
                    }
                }
            }
        }
    }
}
