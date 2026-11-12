using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Iv4xr.SpaceEngineers;
using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.FileSystem;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Iv4xr.SePlugin.Control
{
    /// <summary>
    /// TG: Grids als Blaupause oder Prefab exportieren und wieder einfuegen.
    /// Nutzt nur oeffentliche Teile des Spiels (ModAPI, ObjectBuilder-Serializer).
    /// </summary>
    public class TgWerkzeuge : ITg
    {
        private const double Suchradius = 100.0;

        public string TargetGridId()
        {
            return ZielGrid("").EntityId.ToString();
        }

        public TgExport ExportBlueprint(string gridId, string name)
        {
            name = Name(name);
            var grids = Gruppe(ZielGrid(gridId));

            var bp = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();
            bp.Id = new MyDefinitionId(typeof(MyObjectBuilder_ShipBlueprintDefinition), name);
            bp.CubeGrids = grids.ToArray();
            bp.RespawnShip = false;
            bp.DisplayName = MySession.Static.LocalHumanPlayer?.DisplayName ?? "TG";
            bp.OwnerSteamId = Sync.MyId;

            var defs = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            defs.ShipBlueprints = new[] { bp };

            var pfad = Path.Combine(MyFileSystem.UserDataPath, "Blueprints", "local", name, "bp.sbc");
            return Schreiben(defs, pfad, name, grids);
        }

        public TgExport ExportPrefab(string gridId, string name)
        {
            name = Name(name);
            var grids = Gruppe(ZielGrid(gridId));

            // Groesstes Grid in den Ursprung, die anderen mit gleichem Abstand dazu
            var mitte = (Vector3D)grids[0].PositionAndOrientation.Value.Position;
            foreach (var g in grids)
            {
                var po = g.PositionAndOrientation.Value;
                po.Position = (Vector3D)po.Position - mitte;
                g.PositionAndOrientation = po;
            }

            var prefab = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_PrefabDefinition>();
            prefab.Id = new MyDefinitionId(typeof(MyObjectBuilder_PrefabDefinition), name);
            prefab.CubeGrids = grids.ToArray();
            prefab.RespawnShip = false;

            var defs = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            defs.Prefabs = new[] { prefab };

            var pfad = Path.Combine(MyFileSystem.UserDataPath, "TG-Export", "Prefabs", name + ".sbc");
            return Schreiben(defs, pfad, name, grids);
        }

        public List<string> PasteBlueprint(string name, double distance)
        {
            var pfad = Path.Combine(MyFileSystem.UserDataPath, "Blueprints", "local", Name(name), "bp.sbc");
            var defs = Lesen(pfad);
            if (defs.ShipBlueprints == null || defs.ShipBlueprints.Length == 0)
            {
                throw new InvalidOperationException($"Keine Blaupause in {pfad}");
            }

            return Einfuegen(defs.ShipBlueprints[0].CubeGrids, distance);
        }

        public List<string> PastePrefab(string name, double distance)
        {
            var pfad = Path.Combine(MyFileSystem.UserDataPath, "TG-Export", "Prefabs", Name(name) + ".sbc");
            var defs = Lesen(pfad);
            if (defs.Prefabs == null || defs.Prefabs.Length == 0)
            {
                throw new InvalidOperationException($"Kein Prefab in {pfad}");
            }

            var p = defs.Prefabs[0];
            var grids = p.CubeGrids ?? (p.CubeGrid != null ? new[] { p.CubeGrid } : null);
            return Einfuegen(grids, distance);
        }

        // ---------------------------------------------------------------------------------------------

        private static string Name(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name fehlt");
            }

            var sauber = MyUtils.StripInvalidChars(name.Trim());
            if (sauber.Length == 0 || sauber.Contains("..") || sauber.IndexOfAny(new[] { '\\', '/' }) >= 0)
            {
                throw new ArgumentException($"Ungueltiger Name: {name}");
            }

            return sauber;
        }

        private static MyCubeGrid ZielGrid(string gridId)
        {
            if (!string.IsNullOrEmpty(gridId))
            {
                var grid = MyEntities.GetEntityById(long.Parse(gridId)) as MyCubeGrid;
                if (grid == null)
                {
                    throw new ArgumentException($"Grid {gridId} nicht gefunden");
                }

                return grid;
            }

            var gesteuert = MySession.Static.ControlledEntity as MyCubeBlock;
            if (gesteuert?.CubeGrid != null)
            {
                return gesteuert.CubeGrid;
            }

            var figur = MySession.Static.LocalCharacter;
            if (figur == null)
            {
                throw new InvalidOperationException("Keine Figur");
            }

            var pos = figur.PositionComp.GetPosition();
            var kugel = new BoundingSphereD(pos, Suchradius);
            var naechstes = MyEntities.GetTopMostEntitiesInSphere(ref kugel)
                    .OfType<MyCubeGrid>()
                    .Where(g => g.Physics != null && !g.IsPreview)
                    .OrderBy(g => g.PositionComp.WorldAABB.Distance(pos))
                    .FirstOrDefault();
            if (naechstes == null)
            {
                throw new InvalidOperationException($"Kein Grid im Umkreis von {Suchradius} m");
            }

            return naechstes;
        }

        private static List<MyObjectBuilder_CubeGrid> Gruppe(MyCubeGrid grid)
        {
            var gruppe = new List<IMyCubeGrid>();
            MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Mechanical, gruppe);
            if (gruppe.Count == 0)
            {
                gruppe.Add(grid);
            }

            return gruppe
                    .OfType<MyCubeGrid>()
                    .OrderByDescending(g => g.BlocksCount)
                    .Select(g => (MyObjectBuilder_CubeGrid)g.GetObjectBuilder(true))
                    .ToList();
        }

        private static TgExport Schreiben(MyObjectBuilder_Definitions defs, string pfad, string name,
            List<MyObjectBuilder_CubeGrid> grids)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pfad));
            if (!MyObjectBuilderSerializer.SerializeXML(pfad, false, defs))
            {
                throw new IOException($"Schreiben fehlgeschlagen: {pfad}");
            }

            return new TgExport
            {
                Name = name,
                Path = pfad,
                Grids = grids.Count,
                Blocks = grids.Sum(g => g.CubeBlocks.Count),
                GridNames = grids.Select(g => g.DisplayName).ToList(),
            };
        }

        private static MyObjectBuilder_Definitions Lesen(string pfad)
        {
            if (!File.Exists(pfad))
            {
                throw new FileNotFoundException($"Nicht gefunden: {pfad}");
            }

            MyObjectBuilder_Definitions defs;
            if (!MyObjectBuilderSerializer.DeserializeXML(pfad, out defs) || defs == null)
            {
                throw new IOException($"Lesen fehlgeschlagen: {pfad}");
            }

            return defs;
        }

        private static List<string> Einfuegen(MyObjectBuilder_CubeGrid[] grids, double abstand)
        {
            if (!Sync.IsServer)
            {
                throw new InvalidOperationException("Einfuegen nur im eigenen Spiel (Einzelspieler oder Gastgeber)");
            }

            if (grids == null || grids.Length == 0)
            {
                throw new InvalidOperationException("Keine Grids in der Datei");
            }

            var figur = MySession.Static.LocalCharacter;
            if (figur == null)
            {
                throw new InvalidOperationException("Keine Figur");
            }

            var welt = figur.WorldMatrix;
            var ziel = welt.Translation + welt.Forward * (abstand > 0 ? abstand : 50.0);
            var verschiebung = ziel - (Vector3D)grids[0].PositionAndOrientation.Value.Position;

            foreach (var g in grids)
            {
                var po = g.PositionAndOrientation.Value;
                po.Position = (Vector3D)po.Position + verschiebung;
                g.PositionAndOrientation = po;
                g.LinearVelocity = Vector3.Zero;
                g.AngularVelocity = Vector3.Zero;
            }

            MyAPIGateway.Entities.RemapObjectBuilderCollection(grids);
            var ids = new List<string>();
            foreach (var g in grids)
            {
                IMyEntity e = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(g);
                if (e != null)
                {
                    ids.Add(e.EntityId.ToString());
                }
            }

            return ids;
        }
    }
}
