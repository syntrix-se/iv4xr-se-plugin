using System.Collections.Generic;
using Iv4xr.PluginLib;
using Sandbox.Game.Entities.Cube;
using VRageMath;
using Iv4xr.SpaceEngineers.WorldModel;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using VRage.Game.Entity;

namespace Iv4xr.SePlugin.Control
{
    public class CharacterObservationBuilder
    {
        private readonly BlockEntityBuilder m_entityBuilder;

        internal CharacterObservationBuilder(BlockEntityBuilder entityBuilder)
        {
            m_entityBuilder = entityBuilder;
        }

        public CharacterObservation CreateCharacterObservation(MyCharacter character)
        {
            var result = new CharacterObservation
            {
                Gravity = character.Physics.Gravity.ToPlain(),
                Extent = character.PositionComp.LocalAABB.Size.ToPlain(),
                Camera = new Pose()
                {
                    Position = MySector.MainCamera.Position.ToPlain(),
                    OrientationForward = MySector.MainCamera.ForwardVector.ToPlain(),
                    OrientationUp = MySector.MainCamera.UpVector.ToPlain(),
                },
                JetpackRunning = character.JetpackComp?.TurnedOn ?? false,
                DampenersOn = character.JetpackComp?.DampenersEnabled ?? true,
                HelmetEnabled = character.OxygenComponent.HelmetEnabled,
                LightEnabled = character.LightEnabled,
                CurrentLightPower = character.CurrentLightPower,
                Health = character.StatComp.HealthRatio,
                Oxygen = character.GetSuitGasFillLevel(MyCharacterOxygenComponent.OxygenId),
                Hydrogen = character.GetSuitGasFillLevel(MyCharacterOxygenComponent.HydrogenId),
                SuitEnergy = character.SuitEnergyLevel,
                HeadLocalXAngle = character.HeadLocalXAngle,
                HeadLocalYAngle = character.HeadLocalYAngle,
                TargetBlock = TargetBlock(character),
                TargetUseObject = UseObject(character),
                Movement = (CharacterMovementEnum)character.CurrentMovementState,
                Inventory = character.GetInventory().ToInventory(),
                BootsState = GetBootState(character),
                RelativeDampeningEntity = character.RelativeDampeningEntity.ToEntityOrNull(),
                MovementFlags = (CharacterMovementFlags)((byte)character.MovementFlags),
                JetpackControlThrust =
                        character.JetpackComp?.GetInstanceProperty<MyEntityThrustComponent>("ThrustComp").ControlThrust
                                .ToPlain() ?? new PlainVec3D(),
                JetpackFinalThrust =
                        character.JetpackComp?.FinalThrust.ToPlain() ?? new PlainVec3D(), // null JetpackComp
                CurrentWeapon = WeaponEntity(character).ToPolymorphicEntity(),
            };
            character.ToEntity(result);
            result.Id = character.CharacterId().ToString();
            return result;
        }

        private MyEntity WeaponEntity(MyCharacter character)
        {
            if (character.CurrentWeapon is MyEntity weapon)
            {
                return weapon;
            }

            return null;
        }

        private static BootsState GetBootState(MyCharacter character)
        {
            return character
                    .GetInstanceFieldOrThrow<object>("m_bootsState")
                    .GetInstanceFieldOrThrow<BootsState>("m_value");
        }

        private Block TargetBlock(MyCharacter character)
        {
            return TargetWeaponBlock(character) ?? TargetDetectorBlock(character);
        }

        private Block TargetWeaponBlock(MyCharacter character)
        {
            if (!(character.CurrentWeapon is MyEngineerToolBase wp)) return null;
            var slimBlock = wp.GetTargetBlock() ?? StrahlBlock(character, wp.MaximumShotLength + 1.0);
            return slimBlock == null ? null : m_entityBuilder.CreateAndFill(slimBlock);
        }

        /// <summary>
        /// TG: Rueckfall, wenn das Werkzeug selbst (noch) keinen Block meldet: eigener Sichtstrahl
        /// vom Kopf der Figur in Blickrichtung, Laenge etwa die Reichweite des Werkzeugs.
        /// </summary>
        private static MySlimBlock StrahlBlock(MyCharacter character, double laenge)
        {
            var kopf = character.GetHeadMatrix(false, true);
            var von = kopf.Translation;
            var bis = von + kopf.Forward * System.Math.Max(laenge, 2.5);
            var linie = new LineD(von, bis);
            var treffer = new List<MyLineSegmentOverlapResult<MyEntity>>();
            MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref linie, treffer);
            MySlimBlock naechster = null;
            var abstand = double.MaxValue;
            foreach (var t in treffer)
            {
                if (!(t.Element is MyCubeGrid grid)) continue;
                var zelle = grid.RayCastBlocks(von, bis);
                if (!zelle.HasValue) continue;
                var block = grid.GetCubeBlock(zelle.Value);
                if (block == null) continue;
                var d = Vector3D.DistanceSquared(von, grid.GridIntegerToWorld(zelle.Value));
                if (d < abstand)
                {
                    abstand = d;
                    naechster = block;
                }
            }

            return naechster;
        }

        private Block TargetDetectorBlock(MyCharacter character)
        {
            var detector = character.Components.Get<MyCharacterDetectorComponent>();
            return detector?.UseObject?.Owner is MyCubeBlock block
                    ? m_entityBuilder.CreateAndFill(block.SlimBlock)
                    : null;
        }

        private UseObject UseObject(MyCharacter character)
        {
            var detector = character.Components.Get<MyCharacterDetectorComponent>();
            return detector?.UseObject != null ? EntityBuilder.CreateUseObject(detector.UseObject) : null;
        }
    }
}
