using System;
using System.Reflection;
using Aki.Reflection.Patching;
using Comfort.Common;
using DoorBreach;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using LiteNetLib;
using LiteNetLib.Utils;
using Fika.Core.Coop.Matchmaker;
using Fika.Core.Networking;
using UnityEngine;
using Fika.Core.Coop.Players;

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable CS0169 // The field is never used

namespace BackdoorBandit
{

    internal class ApplyHit : ModulePatch
    {
        private static BallisticCollider collider;
        private static bool isDoor;
        private static bool isCarTrunk;
        private static bool isLootableContainer;
        private static bool hasHitPoints;
        private static bool validDamage;
        private static Hitpoints hitpoints;
        private static Door door;
        private static Trunk carTrunk;
        private static LootableContainer lootContainer;
        protected override MethodBase GetTargetMethod() => typeof(BallisticCollider).GetMethod(nameof(BallisticCollider.ApplyHit));


        [PatchPostfix]
        public static void PatchPostFix(DamageInfo damageInfo, GStruct390 shotID)
        {
            //try catch for random things applying damage that we don't want
            try
            {
                if (ShouldApplyDamage(damageInfo))
                {
                    HandleDamageForEntity(damageInfo, damageInfo.HittedBallisticCollider as BallisticCollider);
                }
            }
            catch { }
        }

        private static bool ShouldApplyDamage(DamageInfo damageInfo)
        {
            return damageInfo.Player != null
                && damageInfo.Player.iPlayer.IsYourPlayer
                && damageInfo.HittedBallisticCollider.HitType != EFT.NetworkPackets.EHitType.Lamp
                && damageInfo.HittedBallisticCollider.HitType != EFT.NetworkPackets.EHitType.Window
                && damageInfo.DamageType != EDamageType.Explosion;
        }

        private static void HandleDamageForEntity(DamageInfo damageInfo, BallisticCollider collider)
        {
            bool isCarTrunk = false;
            bool isLootableContainer = false;
            bool isDoor = false;
            bool hasHitPoints = false;

            if (collider != null)
            {
                isCarTrunk = collider.GetComponentInParent<Trunk>() != null;
                isLootableContainer = collider.GetComponentInParent<LootableContainer>() != null;
                isDoor = collider.GetComponentInParent<Door>() != null;
                hasHitPoints = collider.GetComponentInParent<Hitpoints>() != null;
            }

            if (isCarTrunk && hasHitPoints)
            {
                HandleCarTrunkDamage(damageInfo, collider, ref validDamage);
            }

            if (isLootableContainer && hasHitPoints)
            {
                HandleLootableContainerDamage(damageInfo, collider, ref validDamage);
            }

            if (isDoor && hasHitPoints)
            {
                HandleDoorDamage(damageInfo, collider, ref validDamage);
            }
        }

        #region DamageApplication
        private static void HandleCarTrunkDamage(DamageInfo damageInfo, BallisticCollider collider, ref bool validDamage)
        {
            if (DoorBreachPlugin.OpenCarDoors.Value)
            {
                DamageUtility.CheckCarWeaponAndAmmo(damageInfo, ref validDamage);
            }

            HandleDamage(damageInfo, collider, ref validDamage, "Car Trunk", (hitpoints, entity) =>
            {
                if (hitpoints.hitpoints <= 0)
                {
                    var carTrunk = entity.GetComponentInParent<Trunk>();
                    OpenDoorIfNotAlreadyOpen(carTrunk, damageInfo.Player.AIData.Player, EInteractionType.Open);
                }
            });
        }

        private static void HandleLootableContainerDamage(DamageInfo damageInfo, BallisticCollider collider, ref bool validDamage)
        {
            if (DoorBreachPlugin.OpenLootableContainers.Value)
            {
                DamageUtility.CheckLootableContainerWeaponAndAmmo(damageInfo, ref validDamage);
            }

            HandleDamage(damageInfo, collider, ref validDamage, "Lootable Container", (hitpoints, entity) =>
            {
                if (hitpoints.hitpoints <= 0)
                {
                    var lootContainer = entity.GetComponentInParent<LootableContainer>();
                    OpenDoorIfNotAlreadyOpen(lootContainer, damageInfo.Player.AIData.Player, EInteractionType.Open);
                }
            });
        }

        internal static void HandleDoorDamage(DamageInfo damageInfo, BallisticCollider collider, ref bool validDamage)
        {

            DamageUtility.CheckDoorWeaponAndAmmo(damageInfo, ref validDamage);

            HandleDamage(damageInfo, collider, ref validDamage, "Door", (hitpoints, entity) =>
            {
                if (hitpoints.hitpoints <= 0)
                {
                    var door = entity.GetComponentInParent<Door>();
                    OpenDoorIfNotAlreadyOpen(door, damageInfo.Player.AIData.Player, EInteractionType.Breach);
                }
            });
        }

        internal static void HandleDamage(DamageInfo damageInfo, BallisticCollider collider, ref bool validDamage, string entityName, Action<Hitpoints, GameObject> onHitpointsZero)
        {
            var hitpoints = collider.GetComponentInParent<Hitpoints>() as Hitpoints;

            if (validDamage)
            {
                Logger.LogInfo($"BackdoorBandit: Applying Hit Damage {damageInfo.Damage} hitpoints to {entityName}");
                hitpoints.hitpoints -= damageInfo.Damage;

                onHitpointsZero?.Invoke(hitpoints, collider.gameObject);
            }
        }
        internal static void OpenDoorIfNotAlreadyOpen<T>(T entity, Player player, EInteractionType interactionType) where T : class
        {
            CoopPlayer coopPlayer = player as CoopPlayer;
            if (entity is Door door)
            {
                if (door.DoorState != EDoorState.Open)
                {
                    // Fixes an issue where you can break open doors that have no key assigned to them.
                    if (!DoorBreachPlugin.OpenAnyDoors.Value && door.DoorState == EDoorState.Locked && string.IsNullOrEmpty(door.KeyId) && !door.CanBeBreached)
                    {
                        return;
                    }
                    door.DoorState = EDoorState.Shut;
                    door.KickOpen(true);
                    player.UpdateInteractionCast();

                    // Create packet with info that all players will need
                    SyncOpenStatePacket packet = new SyncOpenStatePacket()
                    {
                        netID = coopPlayer.NetId,
                        objectID = door.Id,
                        objectType = 0
                    };

                    if (MatchmakerAcceptPatches.IsServer)
                    {
                        // Forward the packet to all clients
                        Singleton<FikaServer>.Instance.SendDataToAll(new NetDataWriter(), ref packet,
                            DeliveryMethod.ReliableOrdered);
                        // ReliableOrdered = ensures the packet is received, re-sends it if it fails
                    }
                    else if (MatchmakerAcceptPatches.IsClient)
                    {
                        // If we're a client, send it to the host so they can forward it (Check Plugin.cs for behavior)
                        Singleton<FikaClient>.Instance.SendData(new NetDataWriter(), ref packet,
                            DeliveryMethod.ReliableOrdered);
                    }
                }
            }

            if (entity is LootableContainer container)
            {
                if (container.DoorState != EDoorState.Open)
                {
                    container.DoorState = EDoorState.Shut;
                    container.Open();
                    //player.CurrentManagedState.ExecuteDoorInteraction(container, new InteractionResult(interactionType), null, player);

                    SyncOpenStatePacket packet = new SyncOpenStatePacket()
                    {
                        netID = coopPlayer.NetId,
                        objectID = container.Id,
                        objectType = 1
                    };

                    if (MatchmakerAcceptPatches.IsServer)
                    {
                        Singleton<FikaServer>.Instance.SendDataToAll(new NetDataWriter(), ref packet,
                            DeliveryMethod.ReliableOrdered);
                    }
                    else if (MatchmakerAcceptPatches.IsClient)
                    {
                        Singleton<FikaClient>.Instance.SendData(new NetDataWriter(), ref packet,
                            DeliveryMethod.ReliableOrdered);
                    }
                }
            }
            if (entity is Trunk trunk)
            {

                if (trunk.DoorState != EDoorState.Open)
                {

                    trunk.DoorState = EDoorState.Shut;
                    trunk.Open();
                    //player.CurrentManagedState.ExecuteDoorInteraction(trunk, new InteractionResult(interactionType), null, player);

                    SyncOpenStatePacket packet = new SyncOpenStatePacket()
                    {
                        netID = coopPlayer.NetId,
                        objectID = trunk.Id,
                        objectType = 2
                    };

                    if (MatchmakerAcceptPatches.IsServer)
                    {
                        Singleton<FikaServer>.Instance.SendDataToAll(new NetDataWriter(), ref packet,
                            DeliveryMethod.ReliableOrdered);
                    }
                    else if (MatchmakerAcceptPatches.IsClient)
                    {
                        Singleton<FikaClient>.Instance.SendData(new NetDataWriter(), ref packet,
                            DeliveryMethod.ReliableOrdered);
                    }
                }
            }
        }


        #endregion



    }

}
