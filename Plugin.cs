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

namespace DoorBreach
{
    [BepInPlugin("com.dvize.BackdoorBandit", "dvize.BackdoorBandit", "1.8.2.1")]
    //[BepInDependency("com.spt-aki.core", "3.8.0")]
    public class DoorBreachPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> OpenLootableContainers;
        public static ConfigEntry<bool> OpenCarDoors;
        public static ConfigEntry<bool> OpenAnyDoors;
        public static ConfigEntry<int> ExplosiveTimerSetting;
        public static ConfigEntry<bool> RequireLockHit;
        public static ConfigEntry<int> MinHitPoints;
        public static ConfigEntry<int> MaxHitPoints;
        public static ConfigEntry<bool> WoodDoorShotguns;
        public static ConfigEntry<bool> WoodDoorExplosions;
        public static ConfigEntry<bool> WoodDoorMelee;
        public static ConfigEntry<bool> WoodDoorBreaching;
        public static ConfigEntry<bool> WoodDoorBullets;
        public static ConfigEntry<bool> MetalDoorShotguns;
        public static ConfigEntry<bool> MetalDoorExplosions;
        public static ConfigEntry<bool> MetalDoorMelee;
        public static ConfigEntry<bool> MetalDoorBreaching;
        public static ConfigEntry<bool> MetalDoorBullets;
        public static ConfigEntry<bool> WhitelistMelee;
        public static ConfigEntry<bool> WhitelistWeapons;


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


            OpenLootableContainers = Config.Bind(
                "1. Main Settings",
                "Breach Lootable Containers",
                false,
                new ConfigDescription("If enabled, locked safes can be breached (using the same settings as Metal Doors)",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 5 }));

            OpenCarDoors = Config.Bind(
                "1. Main Settings",
                "Breach Car Doors",
                false,
                new ConfigDescription("If enabled, car doors can be breached (using the same settings as Metal Doors)",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 4 }));

            OpenAnyDoors = Config.Bind(
                "1. Main Settings",
                "Breach Keyless Doors",
                true,
                new ConfigDescription("If enabled, any door (doors that can never be unlocked/have no keys to them) can be breached",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 3 }));

            ExplosiveTimerSetting = Config.Bind(
                "1. Main Settings",
                "Explosive Timer",
                10,
                new ConfigDescription("Time (in seconds) for placed TNT on doors to explode",
                    new AcceptableValueRange<int>(3, 30),
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            RequireLockHit = Config.Bind(
                "1. Main Settings",
                "Require Lock Hit",
                true,
                new ConfigDescription("On doors with handles, bullets must hit near the knob/handle to breach doors",
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

            WoodDoorShotguns = Config.Bind(
                "3. Wood Doors",
                "Enable Shotgun Damage",
                true,
                new ConfigDescription("Allows shotguns to damage + breach wooden doors",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 5 }));

            WoodDoorExplosions = Config.Bind(
                "3. Wood Doors",
                "Enable Explosion Damage",
                true,
                new ConfigDescription("Allows grenades to damage + breach wooden doors",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 4 }));

            WoodDoorMelee = Config.Bind(
                "3. Wood Doors",
                "Enable Melee Damage",
                true,
                new ConfigDescription("Allows certain melee weapons to damage + breach wooden doors",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 3 }));

            WoodDoorBreaching = Config.Bind(
                "3. Wood Doors",
                "OVERRIDE: Allow Breaching Round Damage",
                true,
                new ConfigDescription("Allows breaching rounds to damage and open wooden doors - overrides above settings",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            WoodDoorBullets = Config.Bind(
                "3. Wood Doors",
                "OVERRIDE: Allow Bullet Damage",
                false,
                new ConfigDescription("Allows ANY weapon to damage + breach wooden doors - overrides above settings",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 1 }));

            MetalDoorShotguns = Config.Bind(
                "4. Metal Doors",
                "Enable Shotgun Damage",
                false,
                new ConfigDescription("Allows shotguns to damage + breach metal doors",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 5 }));

            MetalDoorExplosions = Config.Bind(
                "4. Metal Doors",
                "Enable Explosion Damage",
                false,
                new ConfigDescription("Allows grenades to damage + breach metal doors",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 4 }));

            MetalDoorMelee = Config.Bind(
                "4. Metal Doors",
                "Enable Melee Damage",
                false,
                new ConfigDescription("Allows certain melee weapons to damage + breach metal doors",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 3 }));

            MetalDoorBreaching = Config.Bind(
                "4. Metal Doors",
                "OVERRIDE: Allow Breaching Round Damage",
                true,
                new ConfigDescription("Allows breaching rounds to damage and open metal doors",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            MetalDoorBullets = Config.Bind(
                "4. Metal Doors",
                "OVERRIDE: Allow Bullet Damage",
                false,
                new ConfigDescription("Allows ANY weapon to damage + breach metal doors - overwrites above settings",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 1 }));

            WhitelistMelee = Config.Bind(
                "5. Weapon Whitelist",
                "Require Melee In Whitelist",
                true,
                new ConfigDescription("When enabled, melee weapons must be part of the MeleeWeapons.json in order to damage and breach doors",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            WhitelistWeapons = Config.Bind(
                "5. Weapon Whitelist",
                "Require Weapon In Whitelist",
                true,
                new ConfigDescription("When enabled, uses the json files as a whitelist to only allow certain firearms to breach doors (see ShotgunWeapons.json, etc)",
                    null,
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
                        ExplosiveBreachComponent.StartExplosiveBreach(door, player, arg1.TNTTimer, false);
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
                                    container.Open();
                                }

                                break;
                            }
                            case GameObjectType.Trunk:
                            {
                                Trunk trunk = (Trunk)worldInteractiveObject;

                                if (trunk.DoorState != EDoorState.Open)
                                {
                                    trunk.DoorState = EDoorState.Shut;
                                    trunk.Open();
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
