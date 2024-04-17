using System;
using System.Diagnostics;
using System.Reflection;
using Aki.Reflection.Patching;
using BackdoorBandit;
using BackdoorBandit.Patches;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.UI;
using LiteNetLib.Utils;
using LiteNetLib;
using MPT.Core.Coop.Components;
using MPT.Core.Coop.Matchmaker;
using MPT.Core.Coop.Players;
using MPT.Core.Modding;
using MPT.Core.Modding.Events;
using MPT.Core.Networking;
using UnityEngine;
using VersionChecker;
using static GClass1873;
using System.ComponentModel;
using static Streamer;

namespace DoorBreach
{
    [BepInPlugin("com.dvize.BackdoorBandit", "dvize.BackdoorBandit", "1.8.1")]
    //[BepInDependency("com.spt-aki.core", "3.7.6")]
    public class DoorBreachPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> PlebMode;
        public static ConfigEntry<bool> SemiPlebMode;
        public static ConfigEntry<bool> BreachingRoundsOpenMetalDoors;
        public static ConfigEntry<bool> OpenLootableContainers;
        public static ConfigEntry<bool> OpenCarDoors;
        public static ConfigEntry<int> MinHitPoints;
        public static ConfigEntry<int> MaxHitPoints;

        public enum GameObjectType 
        {
            Door,
            Container,
            Trunk
        }

        public static int interactiveLayer;

        private void Awake()
        {
            CheckEftVersion();

            PlebMode = Config.Bind(
                "1. Main Settings",
                "Plebmode",
                false,
                new ConfigDescription("Enabled Means No Requirements To Breach Any Door/LootContainer",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 5 }));

            SemiPlebMode = Config.Bind(
                "1. Main Settings",
                "Semi-Plebmode",
                false,
                new ConfigDescription("Enabled Means Any Round Breach Regular Doors, Not Reinforced doors",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 4 }));

            BreachingRoundsOpenMetalDoors = Config.Bind(
                "1. Main Settings",
                "Breach Rounds Affects Metal Doors",
                false,
                new ConfigDescription("Enabled Means Any Breach Round opens a door",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 3 }));

            OpenLootableContainers = Config.Bind(
                "1. Main Settings",
                "Breach Lootable Containers",
                false,
                new ConfigDescription("If enabled, can use shotgun breach rounds on safes",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            OpenCarDoors = Config.Bind(
                "1. Main Settings",
                "Breach Car Doors",
                false,
                new ConfigDescription("If Enabled, can use shotgun breach rounds on car doors",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 1 }));

            MinHitPoints = Config.Bind(
                "2. Hit Points",
                "Min Hit Points",
                100,
                new ConfigDescription("Minimum Hit Points Required To Breach, Default 100",
                new AcceptableValueRange<int>(0, 1000),
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            MaxHitPoints = Config.Bind(
                "2. Hit Points",
                "Max Hit Points",
                200,
                new ConfigDescription("Maximum Hit Points Required To Breach, Default 200",
                new AcceptableValueRange<int>(0, 2000),
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 1 }));

            //new NewGamePatch().Enable();
            new BackdoorBandit.ApplyHit().Enable();
            new ActionMenuDoorPatch().Enable();
            new ActionMenuKeyCardPatch().Enable();

            MPTEventDispatcher.SubscribeEvent<GameWorldStartedEvent>(OnGameWorldStarted);

            MPTEventDispatcher.SubscribeEvent<MPTClientCreatedEvent>(OnClientCreated);
            MPTEventDispatcher.SubscribeEvent<MPTServerCreatedEvent>(OnServerCreated);
        }

        private void OnGameWorldStarted(GameWorldStartedEvent obj)
        {
            DoorBreachPlugin.interactiveLayer = LayerMask.NameToLayer("Interactive");

            BackdoorBandit.DoorBreachComponent.Enable();
            BackdoorBandit.ExplosiveBreachComponent.Enable();
        }

        private void OnServerCreated(MPTServerCreatedEvent obj)
        {
            obj.Server.packetProcessor.SubscribeNetSerializable<PlantTNTPacket, NetPeer>(OnTNTPacketReceived);
            obj.Server.packetProcessor.SubscribeNetSerializable<SyncOpenStatePacket, NetPeer>(OnSyncOpenStatePacketReceived);
        }
        private void OnClientCreated(MPTClientCreatedEvent obj)
        {
            obj.Client.packetProcessor.SubscribeNetSerializable<PlantTNTPacket, NetPeer>(OnTNTPacketReceived);
            obj.Client.packetProcessor.SubscribeNetSerializable<SyncOpenStatePacket, NetPeer>(OnSyncOpenStatePacketReceived);
        }

        private void OnTNTPacketReceived(PlantTNTPacket arg1, NetPeer arg2)
        {
            if (CoopHandler.TryGetCoopHandler(out CoopHandler coopHandler))
            {
                if (coopHandler.Players.TryGetValue(arg1.profileID, out CoopPlayer player))
                {
                    if (coopHandler.GetInteractiveObject(arg1.doorID, out WorldInteractiveObject worldInteractiveObject))
                    {
                        // We can cast this to a Door since we're sure only a Door type was sent
                        Door door = (Door)worldInteractiveObject;

                        // Run the method on the recipient of this packet
                        ExplosiveBreachComponent.StartExplosiveBreach(door, player, false);
                    }
                }
            }

            if (MatchmakerAcceptPatches.IsServer)
            {
                // If the host receives the packet from a client, now forward this packet to all clients (excluding arg2 - the person who sent it).
                Singleton<MPTServer>.Instance.SendDataToAll(new NetDataWriter(), ref arg1, DeliveryMethod.ReliableOrdered, arg2);
            }
        }

        private void OnSyncOpenStatePacketReceived(SyncOpenStatePacket arg1, NetPeer arg2)
        {
            if (CoopHandler.TryGetCoopHandler(out CoopHandler coopHandler))
            {
                if (coopHandler.Players.TryGetValue(arg1.profileID, out CoopPlayer player))
                {
                    if (coopHandler.GetInteractiveObject(arg1.objectID, out WorldInteractiveObject worldInteractiveObject))
                    {
                        // Convert from int in the packet to the enum above
                        // (Can't send an enum value as part of a packet, apparently)
                        GameObjectType gameObjectType = (GameObjectType)arg1.objectType;

                        switch (gameObjectType)
                        {
                            // Handle logic for ApplyHitPatch.OpenDoorIfNotAlreadyOpen on the recipient
                            case GameObjectType.Door:
                            {
                                Door door = (Door)worldInteractiveObject;

                                if (door.DoorState != EDoorState.Open)
                                {
                                    door.DoorState = EDoorState.Shut;
                                    //player.CurrentManagedState.ExecuteDoorInteraction(container, new InteractionResult(EInteractionType.Breach), null, player);
                                    door.KickOpen(true);
                                    coopHandler.MyPlayer.UpdateInteractionCast();
                                }

                                break;
                            }
                            case GameObjectType.Container:
                            {
                                LootableContainer container = (LootableContainer)worldInteractiveObject;

                                if (container.DoorState != EDoorState.Open)
                                {
                                    container.DoorState = EDoorState.Shut;
                                    // Might want to use something besides ExecuteDoorInteraction to prevent the hand anim from playing for this
                                    // EInteractionType.Open is what's passed to OpenDoorIfNotAlreadyOpen for LootableContainers in ApplyHitPatch
                                    player.CurrentManagedState.ExecuteDoorInteraction(container, new InteractionResult(EInteractionType.Open), null, player);
                                }

                                break;
                            }
                            case GameObjectType.Trunk:
                            {
                                Trunk trunk = (Trunk)worldInteractiveObject;

                                if (trunk.DoorState != EDoorState.Open)
                                {
                                    trunk.DoorState = EDoorState.Shut;
                                    player.CurrentManagedState.ExecuteDoorInteraction(trunk, new InteractionResult(EInteractionType.Open), null, player);
                                }

                                break;
                            }
                        }

                        if (MatchmakerAcceptPatches.IsServer)
                        {
                            Singleton<MPTServer>.Instance.SendDataToAll(new NetDataWriter(), ref arg1, DeliveryMethod.ReliableOrdered, arg2);
                        }
                    }
                }
            }
        }

        private void CheckEftVersion()
        {
            // Make sure the version of EFT being run is the correct version
            int currentVersion = FileVersionInfo.GetVersionInfo(BepInEx.Paths.ExecutablePath).FilePrivatePart;
            int buildVersion = TarkovVersion.BuildVersion;
            if (currentVersion != buildVersion)
            {
                Logger.LogError($"ERROR: This version of {Info.Metadata.Name} v{Info.Metadata.Version} was built for Tarkov {buildVersion}, but you are running {currentVersion}. Please download the correct plugin version.");
                EFT.UI.ConsoleScreen.LogError($"ERROR: This version of {Info.Metadata.Name} v{Info.Metadata.Version} was built for Tarkov {buildVersion}, but you are running {currentVersion}. Please download the correct plugin version.");
                throw new Exception($"Invalid EFT Version ({currentVersion} != {buildVersion})");
            }
        }
    }

    //re-initializes each new game
    internal class NewGamePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));

        [PatchPrefix]
        public static void PatchPrefix()
        {
            //stolen from drakiaxyz - thanks
            DoorBreachPlugin.interactiveLayer = LayerMask.NameToLayer("Interactive");

            BackdoorBandit.DoorBreachComponent.Enable();
            BackdoorBandit.ExplosiveBreachComponent.Enable();
        }
    }
}
