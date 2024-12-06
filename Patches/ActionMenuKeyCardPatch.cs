using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SPT.Reflection.Patching;
using DoorBreach;
using EFT;
using EFT.Interactive;

namespace BackdoorBandit.Patches
{
    internal class ActionMenuKeyCardPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(GetActionsClass).GetMethod(nameof(GetActionsClass.smethod_12));

        // Check if an action is already added. Hopefully door's action takes precedence
        public static bool IsActionAdded(List<ActionsTypesClass> actions, string actionName)
        {
            return actions.Any(action => action.Name.ToLower() == actionName.ToLower());
        }

        [PatchPostfix]
        public static void Postfix(ref ActionsReturnClass __result, GamePlayerOwner owner, Door door)
        {
            if (__result != null && __result.Actions != null && !IsActionAdded(__result.Actions, "Plant Explosive"))
            {
                if (door.DoorState != EDoorState.Open)
                {
                    __result.Actions.Add(new ActionsTypesClass
                    {
                        Name = "Plant Explosive",
                        Action = new Action(() =>
                        {
                            BackdoorBandit.ExplosiveBreachComponent.StartExplosiveBreach(door, owner.Player, DoorBreachPlugin.ExplosiveTimerSetting.Value, true);

                        }),
                        Disabled = (!door.IsBreachAngle(owner.Player.Position) || !BackdoorBandit.ExplosiveBreachComponent.IsValidDoorState(door) ||
                                    !BackdoorBandit.ExplosiveBreachComponent.hasC4Explosives(owner.Player))
                    }); 
                }
            }
        }
    }
}
