using System;
using System.Collections.Generic;
using Comfort.Common;
using DoorBreach;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.InventoryLogic;
using UnityEngine;

namespace BackdoorBandit
{
    internal static class DamageUtility
    {
        internal static void CheckWeaponAndAmmo(DamageInfo damageInfo, ref bool validDamage, ref HashSet<string> validWeapons, Func<AmmoTemplate, bool> isRoundValid, Func<DamageInfo, bool> isValidLockHit)
        {
            var material = damageInfo.HittedBallisticCollider.TypeOfMaterial;
            var weapon = damageInfo.Weapon.TemplateId;
            var damageType = damageInfo.DamageType;

            // Check for melee damage, check if the material is metal and use the proper config value based on what isMetal returns
            if (damageType == EDamageType.Melee && isMetal(material) ? DoorBreachPlugin.MetalDoorMelee.Value : DoorBreachPlugin.WoodDoorMelee.Value)
            {
                // Check if we apply the whitelist
                if (DoorBreachPlugin.WhitelistMelee.Value)
                {
                    // Check if MeleeWeapons.json doesn't have the melee weapon used
                    if (!DoorBreachComponent.MeleeWeapons.Contains(weapon))
                    {
                        // MeleeWeapons.json doesn't contain our weapon, so we return and keep validDamage as false
                        return;
                    }
                    // Otherwise, it's valid damage
                    validDamage = true;
                    return;
                }
                // In the case we don't have the whitelist for melee enabled, we skip checking it and mark it as valid
                validDamage = true;
                return;
            }
            // Checking for explosive damage
            else if (damageType == EDamageType.GrenadeFragment && isMetal(material) ? DoorBreachPlugin.MetalDoorExplosions.Value : DoorBreachPlugin.WoodDoorExplosions.Value)
            {
                validDamage = true;
                return;
            }
            // Checking for bullet damange
            else if (damageType == EDamageType.Bullet)
            {
                var bulletTemplate = Singleton<ItemFactory>.Instance.ItemTemplates[damageInfo.SourceId] as AmmoTemplate;
#if DEBUG
            DoorBreachComponent.Logger.LogInfo($"ammoTemplate: {bulletTemplate.Name}");
            DoorBreachComponent.Logger.LogInfo($"BB: Actual DamageType is : {damageInfo.DamageType}");
            DoorBreachComponent.Logger.LogInfo($"isValidLockHit: {isValidLockHit(damageInfo)}");
            DoorBreachComponent.Logger.LogInfo($"isRoundValid: {isRoundValid(bulletTemplate)}");
            DoorBreachComponent.Logger.LogInfo($"weapon used: {damageInfo.Weapon.LocalizedName()}, id: {damageInfo.Weapon.TemplateId}");
            DoorBreachComponent.Logger.LogInfo($"validWeapons Contains weapon tpl id: {validWeapons.Contains(weapon).ToString()}");
#endif
                // Checking the weapon whitelist
                if (DoorBreachPlugin.WhitelistWeapons.Value)
                {
                    if (!validWeapons.Contains(weapon))
                    {
                        return;
                    }
                }

                // Config option for requiring a valid lock hit
                if (DoorBreachPlugin.RequireLockHit.Value)
                {
                    if (!isValidLockHit(damageInfo))
                    {
                        return;
                    }
                }

                // Breaching slug override
                // If the config option for breaching slugs isn't enabled, it will fall through and be treated as any other shotgun
                if (isBreachingSlug(bulletTemplate) && isMetal(material) ? DoorBreachPlugin.MetalDoorBreaching.Value : DoorBreachPlugin.WoodDoorBreaching.Value)
                {
                    validDamage = true;
                    return;
                }

                // Check if we're using a shotgun
                if (isShotgun(damageInfo) && isMetal(material) ? DoorBreachPlugin.MetalDoorShotguns.Value : DoorBreachPlugin.WoodDoorShotguns.Value)
                {
                    validDamage = true;
                    return;
                }

                //  We aren't using a shotgun at this point, so check if the config option allows for any bullet type to damage doors
                if (isMetal(material) ? DoorBreachPlugin.MetalDoorBullets.Value : DoorBreachPlugin.WoodDoorBullets.Value)
                {
                    validDamage = true;
                    if (isValidLockHit == isValidCarTrunkLockHit)
                    {
                        damageInfo.Damage = 500;  //only so it opens the car trunk in one shot
                    }
                    return;
                }
            }
            validDamage = false;
            return;

        }

        internal static void CheckDoorWeaponAndAmmo(DamageInfo damageInfo, ref bool validDamage)
        {
            CheckWeaponAndAmmo(damageInfo, ref validDamage, ref DoorBreachComponent.ApplicableWeapons,
               ammo => isHEGrenade(ammo) || isShrapnel(ammo) || isBreachingSlug(ammo), isValidDoorLockHit);
        }

        internal static void CheckCarWeaponAndAmmo(DamageInfo damageInfo, ref bool validDamage)
        {
            CheckWeaponAndAmmo(damageInfo, ref validDamage, ref DoorBreachComponent.ApplicableWeapons,
                ammo => isHEGrenade(ammo) || isShrapnel(ammo) || isBreachingSlug(ammo), isValidCarTrunkLockHit);
        }

        internal static void CheckLootableContainerWeaponAndAmmo(DamageInfo damageInfo, ref bool validDamage)
        {
            CheckWeaponAndAmmo(damageInfo, ref validDamage, ref DoorBreachComponent.ApplicableWeapons,
                ammo => isHEGrenade(ammo) || isShrapnel(ammo) || isBreachingSlug(ammo), isValidContainerLockHit);
        }

        internal static bool isShrapnel(AmmoTemplate bulletTemplate)
        {
            //check if bulletTemplate is shrapnel and we only want grenade shrapnel not bullet shrapnel
            //bulletTemplate._id = "5b44e3f4d4351e003562b3f4";
            return (bulletTemplate.FragmentType == "5485a8684bdc2da71d8b4567");
        }

        internal static bool isHEGrenade(AmmoTemplate bulletTemplate)
        {
            //check if bulletTemplate is HE Grenade if has ExplosionStrength and only one projectile
            return (bulletTemplate.ExplosionStrength > 0
                && bulletTemplate.ProjectileCount == 1);
        }

        internal static bool isBreachingSlug(AmmoTemplate bulletTemplate)
        {
            //doorbreach id: 660249a0712c1005a4a3ab41

            return (bulletTemplate._id == "660249a0712c1005a4a3ab41");
        }
        internal static bool isShotgun(DamageInfo damageInfo)
        {
            //check if weapon is a shotgun

            return ((damageInfo.Weapon as Weapon)?.WeapClass == "shotgun");
        }
        internal static bool isMetal(MaterialType material)
        {
            //check if the object hit is made of metal

            return (material == MaterialType.MetalThin || material == MaterialType.MetalThick);
        }
        internal static bool isValidDoorLockHit(DamageInfo damageInfo)
        {
            //check if door handle area was hit
            Collider col = damageInfo.HitCollider;

            //if doorhandle exists and is hit
            if (col.GetComponentInParent<Door>().GetComponentInChildren<DoorHandle>() != null)
            {
                Vector3 localHitPoint = col.transform.InverseTransformPoint(damageInfo.HitPoint);
                DoorHandle doorHandle = col.GetComponentInParent<Door>().GetComponentInChildren<DoorHandle>();
                Vector3 doorHandleLocalPos = doorHandle.transform.localPosition;
                float distanceToHandle = Vector3.Distance(localHitPoint, doorHandleLocalPos);
                return distanceToHandle < 0.25f;
            }
            //if doorhandle does not exist then it is a valid hit
            else
            {
                return true;
            }

        }

        internal static bool isValidCarTrunkLockHit(DamageInfo damageInfo)
        {
            //check if door handle area was hit
            Collider col = damageInfo.HitCollider;

            //if doorhandle exists and is hit
            if (col.GetComponentInParent<Trunk>().GetComponentInChildren<DoorHandle>() != null)
            {
                var gameobj = col.GetComponentInParent<Trunk>().gameObject;

                //find child game object Lock from gameobj
                var carLockObj = gameobj.transform.Find("CarLock_Hand").gameObject;
                var lockObj = carLockObj.transform.Find("Lock").gameObject;

                float distanceToLock = Vector3.Distance(damageInfo.HitPoint, lockObj.transform.position);

                return distanceToLock < 0.25f;
            }
            //if doorhandle does not exist then it is a valid hit
            else
            {
                return true;
            }

        }

        internal static bool isValidContainerLockHit(DamageInfo damageInfo)
        {
            //check if door handle area was hit
            Collider col = damageInfo.HitCollider;

            //if doorhandle exists and is hit
            if (col.GetComponentInParent<LootableContainer>().GetComponentInChildren<DoorHandle>() != null)
            {
                var gameobj = col.GetComponentInParent<LootableContainer>().gameObject;

                //find child game object Lock from gameobj
                var lockObj = gameobj.transform.Find("Lock").gameObject;

                float distanceToLock = Vector3.Distance(damageInfo.HitPoint, lockObj.transform.position);
                return distanceToLock < 0.25f;
            }
            //if doorhandle does not exist then it is a valid hit
            else
            {

                return true;
            }

        }

    }
}
