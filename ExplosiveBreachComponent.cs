using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Systems.Effects;
using UnityEngine;
using LiteNetLib.Utils;
using Fika.Core.Coop.Matchmaker;
using Fika.Core.Networking;
using LiteNetLib;
using Fika.Core.Coop.Players;
using DoorBreach;

namespace BackdoorBandit
{
    internal class ExplosiveBreachComponent : MonoBehaviour
    {
        internal static Player player;
        internal static GameWorld gameWorld;
        internal static List<C4Instance> c4Instances;
        private static ExplosiveBreachComponent componentInstance;
        //private static readonly string TNTTemplateId = "60391b0fb847c71012789415";
        private static readonly string C4ExplosiveId = "6636606320e842b50084e51a";
        private static Vector2 _impactsGagRadius;
        private static Effects effectsInstance;
        private static CameraClass cameraInstance;
        private static BetterAudio betterAudioInstance;
        internal static ManualLogSource Logger
        {
            get; private set;
        }

        private ExplosiveBreachComponent()
        {
            if (Logger == null)
            {
                Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(ExplosiveBreachComponent));
            }
        }

        private void Start()
        {
            //initialize variables
            c4Instances = new List<C4Instance>();
            componentInstance = this;
            gameWorld = Singleton<GameWorld>.Instance;
            player = gameWorld.MainPlayer;
            _impactsGagRadius = new Vector2(1f, 3f);
            effectsInstance = Singleton<Effects>.Instance;
            cameraInstance = CameraClass.Instance;
            betterAudioInstance = Singleton<BetterAudio>.Instance;
        }
        internal static bool hasC4Explosives(Player player)
        {
            // Search playerItems for first c4 explosive
            var foundItem = player.Inventory.GetPlayerItems(EPlayerItems.Equipment).FirstOrDefault(x => x.TemplateId == C4ExplosiveId);

            if (foundItem != null)
            {
                return true;
            }

            return false;
        }

        internal static bool IsValidDoorState(Door door) =>
                   door.DoorState == EDoorState.Shut || door.DoorState == EDoorState.Locked || door.DoorState == EDoorState.Breaching;

        internal static void StartExplosiveBreach(Door door, Player player, int timer, bool local)
        {
            if (door == null || player == null)
            {
                Logger.LogError("Either the door or Player is null. Can't start breach.");
                return;
            }

            CoopPlayer coopPlayer = player as CoopPlayer;
            if (local)
            {
                PlantC4Packet packet = new PlantC4Packet()
                {
                    netID = coopPlayer.NetId,
                    doorID = door.Id,
                    C4Timer = timer,
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
            TryPlaceC4OnDoor(door, player);

            RemoveItemFromPlayerInventory(player);

            // Ensure we have a reference to the ExplosiveBreachComponent.
            if (componentInstance == null)
            {
                componentInstance = gameWorld.GetComponent<ExplosiveBreachComponent>();
                if (componentInstance == null)
                {
                    componentInstance = gameWorld.gameObject.AddComponent<ExplosiveBreachComponent>();
                }
            }

            // Start a coroutine for the most recently placed TNT.
            if (c4Instances.Any())
            {
                var latestC4Instance = c4Instances.Last();
                StartDelayedExplosionCoroutine(door, player, timer, componentInstance, latestC4Instance);
            }
        }

        private static void TryPlaceC4OnDoor(Door door, Player player)
        {
            var itemFactory = Singleton<ItemFactory>.Instance;
            var c4Item = itemFactory.CreateItem(MongoID.Generate(), C4ExplosiveId, null);

            // Find the "Lock" GameObject instead of using the DoorHandle
            Transform lockTransform = door.transform.Find("Lock");
            if (lockTransform == null)
            {
                Logger.LogError("Lock component not found.");
                return; // Exit if the Lock component is not found
            }
            Vector3 lockPosition = lockTransform.position;
            Vector3 playerPosition = player.Transform.position;

            // Calculate the vector from the door (lock position) towards the player
            Vector3 doorToPlayer = playerPosition - lockPosition;
            doorToPlayer.y = 0; // Remove the vertical component to ensure the C4 faces horizontally

            // Normalize the vector to ensure it's a proper direction vector
            Vector3 doorForward = doorToPlayer.normalized;

            // Determine placement position just off the surface of the door, near the lock
            float doorThickness = 0.07f; // Adjust this value as needed
            Vector3 c4Position = lockPosition + doorForward * doorThickness; // Placing it slightly forward

            // Rotate the forward vector to face towards the player correctly
            Quaternion rotation = Quaternion.LookRotation(doorForward, Vector3.up);

            // Apply a 90-degree rotation around the y-axis if the C4's front is not oriented correctly
            Quaternion correctionRotation = Quaternion.Euler(90, 0, 0);

            rotation *= correctionRotation;

            // Place the C4 item in the game world
            LootItem lootItem = gameWorld.SetupItem(c4Item, player.InteractablePlayer, c4Position, rotation);

            c4Instances.Add(new C4Instance(lootItem, c4Position));
        }


        private static void RemoveItemFromPlayerInventory(Player player)
        {
            var foundItem = player.Inventory.GetPlayerItems(EPlayerItems.Equipment).FirstOrDefault(x => x.TemplateId == C4ExplosiveId);
            if (foundItem == null) return;

            var traderController = (TraderControllerClass)foundItem.Parent.GetOwner();
            var discardResult = InteractionsHandlerClass.Discard(foundItem, traderController, false, false);

            if (discardResult.Error != null)
            {
                Logger.LogError($"Couldn't remove item: {discardResult.Error}");
                return;
            }

            discardResult.Value.RaiseEvents(traderController, CommandStatus.Begin);
            discardResult.Value.RaiseEvents(traderController, CommandStatus.Succeed);
        }

        private static void StartDelayedExplosionCoroutine(Door door, Player player, int timer, MonoBehaviour monoBehaviour, C4Instance c4Instance)
        {
            if (c4Instance?.LootItem == null)
            {
                Logger.LogError("C4 instance or LootItem is null.");
                return;
            }
            c4Instance.ExplosionCoroutine = monoBehaviour.StartCoroutine(DelayedExplosion(door, player, timer, c4Instance));
        }
        private static void StopExplosionCoroutine(C4Instance c4Instance)
        {
            if (componentInstance != null && c4Instance?.ExplosionCoroutine != null)
            {
                componentInstance.StopCoroutine(c4Instance.ExplosionCoroutine);
                c4Instance.ExplosionCoroutine = null;
                //Logger.LogInfo("Coroutine stopped successfully.");
            }
            else
            {
                Logger.LogError("Failed to stop coroutine: component or coroutine reference is null.");
            }
        }

        private static IEnumerator DelayedExplosion(Door door, Player player, int timer, C4Instance c4Instance)
        {
            // Wait for x seconds.
            float waitTime = timer;
            float currentTime = 0;

            //Logger.LogWarning("Coroutine started.");
            while (currentTime < waitTime)
            {
                //Logger.LogInfo("Checking C4 status...");
                yield return new WaitForSeconds(1);
                currentTime += 1;

                // Check if the C4 object or any of its critical components have been destroyed or are null
                if (c4Instance == null || c4Instance.LootItem == null || c4Instance.LootItem.Item == null || !ExistsInGame(c4Instance.LootItem.Item.Id))
                {
                    //Logger.LogError("C4 instance or related item is null or no longer exists in the game world.");
                    StopExplosionCoroutine(c4Instance);
                    yield break;
                }
            }

            //delete TNT from gameWorld
            if (c4Instance.LootItem != null && c4Instance.LootItem.gameObject != null)
            {
                // Apply explosion effect
                effectsInstance.EmitGrenade("big_explosion", c4Instance.LootItem.transform.position, Vector3.forward, DoorBreachPlugin.explosionRadius.Value);

                if (DoorBreachPlugin.explosionDoesDamage.Value)
                {
                    //apply damage to nearby players based on emission radius
                    float explosionRadius = DoorBreachPlugin.explosionRadius.Value;
                    float baseDamage = DoorBreachPlugin.explosionDamage.Value;
                    Vector3 explosionPosition = c4Instance.LootItem.transform.position;

                    Collider[] hitColliders = Physics.OverlapSphere(explosionPosition, explosionRadius);
                    foreach (Collider hitCollider in hitColliders)
                    {
                        Player tempplayer = hitCollider.GetComponentInParent<Player>();
                        if (tempplayer != null)
                        {
                            float distance = Vector3.Distance(hitCollider.transform.position, explosionPosition);

                            if (CheckLineOfSight(explosionPosition, hitCollider.transform.position))
                            {
                                float damageMultiplier = Mathf.Clamp01(1 - distance / explosionRadius);
                                float damageAmount = baseDamage * damageMultiplier;

                                DamageInfo damageInfo = new DamageInfo
                                {
                                    DamageType = EDamageType.Explosion,
                                    Damage = damageAmount,
                                    Direction = (tempplayer.Transform.position - explosionPosition).normalized,
                                    HitPoint = tempplayer.Transform.position,
                                    HitNormal = -(tempplayer.Transform.position - explosionPosition).normalized,
                                    Player = null,
                                    Weapon = null,
                                    ArmorDamage = damageAmount * 0.5f,
                                };

                                tempplayer.ApplyDamageInfo(damageInfo, EBodyPart.Chest, EBodyPartColliderType.Pelvis, 0f);
                            }
                        }
                    }

                }

                door.KickOpen(true);

                //delete C4 from gameWorld
                UnityEngine.Object.Destroy(c4Instance.LootItem.gameObject);

                if (DoorBreachPlugin.explosionDestroysDoor.Value)
                {
                    //delete door from gameWorld
                    UnityEngine.Object.Destroy(door.gameObject);
                }

                // Clean up references
                if (c4Instances.Contains(c4Instance))
                {
                    c4Instances.Remove(c4Instance);
                }
            }
        }

        private static bool ExistsInGame(string id)
        {
            return gameWorld.FindItemById(id).Value != null;
        }

        private static bool CheckLineOfSight(Vector3 explosionPosition, Vector3 playerPosition)
        {
            RaycastHit hit;
            Vector3 direction = playerPosition - explosionPosition;
            if (Physics.Raycast(explosionPosition, direction.normalized, out hit, direction.magnitude))
            {
                return hit.collider.GetComponent<Player>() != null;
            }
            return false;
        }

        public static void Enable()
        {
            if (Singleton<IBotGame>.Instantiated)
            {
                var gameWorld = Singleton<GameWorld>.Instance;
                gameWorld.GetOrAddComponent<ExplosiveBreachComponent>();
            }

        }


    }



}

