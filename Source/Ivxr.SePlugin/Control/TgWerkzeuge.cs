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

        public TgGridReport GridReport(string gridId)
        {
            var grid = ZielGrid(gridId);
            var bericht = new TgGridReport
            {
                Name = grid.DisplayName,
                IsStatic = grid.IsStatic,
                Blocks = grid.BlocksCount,
                TerminalBlocks = new List<TgBlockReport>(),
            };

            var bloecke = grid.GetFatBlocks().OfType<Sandbox.ModAPI.IMyTerminalBlock>().ToList();

            // Bezugsinventar: ein TG-Lager-Controller (Lager vor Erz und Munition), sonst das groesste
            // Inventar eines Containers, sonst irgendein groesstes Inventar
            IMyInventory bezug = null;
            Sandbox.ModAPI.IMyTerminalBlock bezugBlock = null;
            foreach (var b in bloecke.Where(b => b.HasInventory))
            {
                var inv = b.GetInventory(0);
                var rang = BezugsRang(b);
                var bisherRang = bezugBlock == null ? -1 : BezugsRang(bezugBlock);
                if (bezug == null || rang > bisherRang ||
                    (rang == bisherRang && (double)inv.MaxVolume > (double)bezug.MaxVolume))
                {
                    bezug = inv;
                    bezugBlock = b;
                }
            }

            bericht.ReferenceInventory = bezugBlock?.CustomName;

            foreach (var b in bloecke)
            {
                var slim = ((MyCubeBlock)b).SlimBlock;
                var e = new TgBlockReport
                {
                    Name = b.CustomName,
                    Type = b.BlockDefinition.TypeIdString.Replace("MyObjectBuilder_", ""),
                    Subtype = b.BlockDefinition.SubtypeName,
                    Cell = $"{slim.Min.X},{slim.Min.Y},{slim.Min.Z}",
                    Functional = b.IsFunctional,
                    Working = b.IsWorking,
                    Enabled = (b as Sandbox.ModAPI.IMyFunctionalBlock)?.Enabled,
                };

                if (b.HasInventory)
                {
                    var teile = new List<string>();
                    for (var i = 0; i < b.InventoryCount; i++)
                    {
                        var inv = b.GetInventory(i);
                        var items = new List<VRage.Game.ModAPI.Ingame.MyInventoryItem>();
                        inv.GetItems(items);
                        teile.Add($"[{(double)inv.CurrentVolume * 1000:0}/{(double)inv.MaxVolume * 1000:0} L" +
                                  (items.Count > 0
                                          ? ": " + string.Join(", ", items.Select(it =>
                                                  $"{(double)it.Amount:0.##} {it.Type.SubtypeId}"))
                                          : "") + "]");
                    }

                    e.Inventory = string.Join(" ", teile);
                    if (bezug != null && b != bezugBlock)
                    {
                        var verbunden = false;
                        for (var i = 0; i < b.InventoryCount && !verbunden; i++)
                        {
                            verbunden = b.GetInventory(i).IsConnectedTo(bezug);
                        }

                        e.ConveyorToReference = verbunden;
                    }
                }

                var details = new List<string>();
                if (b is Sandbox.ModAPI.IMyBatteryBlock akku)
                {
                    bericht.BatteryStoredMWh += akku.CurrentStoredPower;
                    bericht.BatteryMaxMWh += akku.MaxStoredPower;
                    details.Add($"Ladung {akku.CurrentStoredPower:0.###}/{akku.MaxStoredPower:0.###} MWh, {akku.ChargeMode}");
                }

                if (b is Sandbox.ModAPI.IMyPowerProducer erzeuger)
                {
                    bericht.PowerOutputMW += erzeuger.CurrentOutput;
                    bericht.PowerMaxMW += erzeuger.MaxOutput;
                    details.Add($"Leistung {erzeuger.CurrentOutput:0.###}/{erzeuger.MaxOutput:0.###} MW");
                }

                if (b is global::SpaceEngineers.Game.ModAPI.IMyAirVent luft)
                {
                    details.Add($"Raum dicht {luft.CanPressurize}, O2 {luft.GetOxygenLevel() * 100:0} %, {luft.Status}");
                }

                if (b is Sandbox.ModAPI.IMyDoor tuer)
                {
                    details.Add($"Tuer {tuer.Status}");
                }

                if (b is Sandbox.ModAPI.IMyProductionBlock prod)
                {
                    details.Add($"produziert {prod.IsProducing}, Warteschlange leer {prod.IsQueueEmpty}");
                }

                e.Detail = details.Count > 0 ? string.Join("; ", details) : null;
                bericht.TerminalBlocks.Add(e);
            }

            return bericht;
        }

        /// <summary>Rang fuer das Bezugsinventar: TG-Lager-Controller 3 (Lager) bzw. 2 (Erz, Munition),
        /// andere Frachtcontainer 1, sonst 0.</summary>
        private static int BezugsRang(Sandbox.ModAPI.IMyTerminalBlock b)
        {
            var sub = b.BlockDefinition.SubtypeName ?? "";
            if (sub.StartsWith("TG_StorageController_")) return 3;
            if (sub.StartsWith("TG_OreController_") || sub.StartsWith("TG_AmmoController_")) return 2;
            return b is Sandbox.ModAPI.IMyCargoContainer ? 1 : 0;
        }

        public List<string> RemoveGrid(string gridId)
        {
            if (!Sync.IsServer)
            {
                throw new InvalidOperationException("Entfernen nur im eigenen Spiel (Einzelspieler oder Gastgeber)");
            }

            if (string.IsNullOrWhiteSpace(gridId))
            {
                throw new ArgumentException("Grid-Kennung fehlt");
            }

            var grid = ZielGrid(gridId);
            var gruppe = new List<IMyCubeGrid>();
            MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Mechanical, gruppe);
            if (gruppe.Count == 0)
            {
                gruppe.Add(grid);
            }

            var gesteuert = MySession.Static.ControlledEntity as MyCubeBlock;
            if (gesteuert != null && gruppe.Contains(gesteuert.CubeGrid))
            {
                throw new InvalidOperationException("Die Figur sitzt in diesem Grid; erst aussteigen");
            }

            var weg = new List<string>();
            foreach (var g in gruppe.OfType<MyCubeGrid>())
            {
                weg.Add(g.EntityId.ToString());
                g.Close();
            }

            return weg;
        }

        public void RenameGrid(string gridId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name fehlt");
            }

            ((IMyCubeGrid)ZielGrid(gridId)).CustomName = name.Trim();
        }

        /// <summary>
        /// Chat-Befehl wie getippt: MyAPIUtilities.EnterMessage ist der Weg, ueber den der Spiel-Chat Eingaben an die
        /// Mods gibt (MessageEntered/MessageEnteredSender). Absender ist der eigene Spieler.
        /// </summary>
        public bool Chat(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text fehlt");
            }

            var utilities = MyAPIGateway.Utilities as MyAPIUtilities;
            if (utilities == null)
            {
                throw new InvalidOperationException("MyAPIGateway.Utilities ist nicht verfuegbar");
            }

            var weiter = true;
            utilities.EnterMessage(Sync.MyId, text.Trim(), ref weiter);
            MyLog.Default.WriteLine("TG iv4XR: Chat-Befehl " + text.Trim() + (weiter ? " (von keiner Mod abgefangen)" : ""));
            return weiter;
        }

        public void RenameBlock(string gridId, int x, int y, int z, string name)
        {
            var block = BlockAnZelle(gridId, x, y, z).FatBlock as Sandbox.ModAPI.IMyTerminalBlock;
            if (block == null)
            {
                throw new ArgumentException($"An {x},{y},{z} steht kein Terminal-Block");
            }

            block.CustomName = name;
        }

        public void AddItem(string gridId, int x, int y, int z, string typeId, string subtypeId, double amount)
        {
            if (!Sync.IsServer)
            {
                throw new InvalidOperationException("Nur im eigenen Spiel (Einzelspieler oder Gastgeber)");
            }

            var block = BlockAnZelle(gridId, x, y, z).FatBlock;
            if (block == null || !block.HasInventory)
            {
                throw new ArgumentException($"An {x},{y},{z} steht kein Block mit Inventar");
            }

            var id = new MyDefinitionId(MyObjectBuilderType.Parse(
                typeId.StartsWith("MyObjectBuilder_") ? typeId : "MyObjectBuilder_" + typeId), subtypeId);
            var ob = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(id);
            var inv = (IMyInventory)block.GetInventory(0);
            var menge = (VRage.MyFixedPoint)amount;
            if (!inv.CanItemsBeAdded(menge, id))
            {
                throw new InvalidOperationException("Passt nicht ins Inventar");
            }

            inv.AddItems(menge, ob);
        }

        private static Sandbox.Game.Entities.Cube.MySlimBlock BlockAnZelle(string gridId, int x, int y, int z)
        {
            var grid = ZielGrid(gridId);
            var block = grid.GetCubeBlock(new Vector3I(x, y, z));
            if (block == null)
            {
                throw new ArgumentException($"An {x},{y},{z} steht kein Block");
            }

            return block;
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
