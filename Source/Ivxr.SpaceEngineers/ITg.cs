using System.Collections.Generic;

namespace Iv4xr.SpaceEngineers
{
    /// <summary>
    /// TG-Erweiterungen (TinoGaming Overhaul). Methoden heissen im Protokoll "Tg.&lt;Name&gt;".
    /// gridId leer = Grid, in dem die Figur sitzt, sonst das naechste Grid im Umkreis von 100 m.
    /// Exportiert wird immer die mechanisch verbundene Gruppe (Rotor, Gelenk, Kolben), groesstes Grid zuerst.
    /// </summary>
    public interface ITg
    {
        /// <summary>Kennung des Grids, das bei leerem gridId genommen wuerde.</summary>
        string TargetGridId();

        /// <summary>Schreibt %APPDATA%\SpaceEngineers\Blueprints\local\&lt;name&gt;\bp.sbc wie das Spiel (Weltkoordinaten bleiben).</summary>
        TgExport ExportBlueprint(string gridId, string name);

        /// <summary>Schreibt %APPDATA%\SpaceEngineers\TG-Export\Prefabs\&lt;name&gt;.sbc; das groesste Grid liegt im Ursprung.</summary>
        TgExport ExportPrefab(string gridId, string name);

        /// <summary>Fuegt die lokale Blaupause &lt;name&gt; im Abstand distance (m) vor der Figur ein. Nur im eigenen Spiel.</summary>
        List<string> PasteBlueprint(string name, double distance);

        /// <summary>Fuegt TG-Export\Prefabs\&lt;name&gt;.sbc im Abstand distance (m) vor der Figur ein. Nur im eigenen Spiel.</summary>
        List<string> PastePrefab(string name, double distance);
    }

    public class TgExport
    {
        public string Name;
        public string Path;
        public int Grids;
        public int Blocks;
        public List<string> GridNames;
    }
}
