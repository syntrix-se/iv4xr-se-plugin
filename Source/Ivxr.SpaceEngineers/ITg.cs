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

        /// <summary>Zustand eines Grids: Strom, Luft, Inventare und ob sie ueber Foerderer mit dem groessten Container verbunden sind.</summary>
        TgGridReport GridReport(string gridId);

        /// <summary>Setzt den Namen des Grids.</summary>
        void RenameGrid(string gridId, string name);

        /// <summary>Setzt den Namen eines Blocks im Grid, gefunden ueber eine Zelle, die er belegt (x, y, z).</summary>
        void RenameBlock(string gridId, int x, int y, int z, string name);

        /// <summary>Legt amount Stueck des Gegenstands (z. B. Ore/Ice) in das erste Inventar des Blocks an Zelle (x, y, z). Nur im eigenen Spiel.</summary>
        void AddItem(string gridId, int x, int y, int z, string typeId, string subtypeId, double amount);
    }

    public class TgExport
    {
        public string Name;
        public string Path;
        public int Grids;
        public int Blocks;
        public List<string> GridNames;
    }
    public class TgGridReport
    {
        public string Name;
        public bool IsStatic;
        public int Blocks;
        public double PowerOutputMW;
        public double PowerMaxMW;
        public double BatteryStoredMWh;
        public double BatteryMaxMWh;
        public string ReferenceInventory;
        public List<TgBlockReport> TerminalBlocks;
    }

    public class TgBlockReport
    {
        public string Name;
        public string Type;
        public string Subtype;
        public string Cell;
        public bool Functional;
        public bool Working;
        public bool? Enabled;
        public bool? ConveyorToReference;
        public string Inventory;
        public string Detail;
    }
}
