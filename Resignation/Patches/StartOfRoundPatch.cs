using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;
using System.Linq.Expressions;

namespace Resignation.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]

    internal class StartOfRoundPatch
    {
        private static InteractTrigger OpenDoorTrigger;
        private static InteractTrigger BeginResignationTrigger;

        static UnityAction<PlayerControllerB> ButtonEvent;

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void AppendResignationTrigger(StartOfRound __instance)
        {
            Resignation.Logger.LogInfo("Instance name #1: " + __instance.gameObject.name);
            Resignation.Logger.LogInfo("Instance name #2: " + StartOfRound.Instance.gameObject.name);
            
            Resignation.Logger.LogInfo(GameObject.Find("Systems/GameSystems").transform.GetChild(1).gameObject.AddComponent<EjectionFlag>());

            Transform DoorPanel = GameObject.Find("Environment/HangarShip/AnimatedShipDoor/HangarDoorButtonPanel").transform;
            Transform button = DoorPanel.Find("StartButton").Find("Cube (2)");

            OpenDoorTrigger = button.GetComponent<InteractTrigger>();

            // OpenDoorTrigger.holdInteraction = true;
            // OpenDoorTrigger.timeToHold = 4f;

            OpenDoorTrigger.onInteract.AddListener((player) =>
            {
                StartResignation(player);
            });
        }

        static void StartResignation(PlayerControllerB controller)
        {
            if (!StartOfRound.Instance.shipHasLanded)
            {
                Resignation.Logger.LogInfo(StartOfRound.Instance.gameObject.GetComponent<EjectionFlag>());
                HUDManager.Instance.AddTextToChatOnServer("Resignation submitted");
                StartOfRound.Instance.ManuallyEjectPlayersServerRpc();

            }

        }

        class EjectionFlag : Component
        {

        }


        static FieldInfo speakerAudioSourceField = AccessTools.Field(typeof(StartOfRound), nameof(StartOfRound.speakerAudioSource));
        static MethodInfo gameObjectMethod = AccessTools.Method(typeof(Component), "get_gameObject");
        static MethodInfo getComponentMethod = AccessTools.Method(typeof(GameObject), nameof(GameObject.GetComponent)).MakeGenericMethod(new System.Type[] {typeof(EjectionFlag)});


        [HarmonyTranspiler]
        [HarmonyPatch(nameof(StartOfRound.Instance.playersFiredGameOver), MethodType.Enumerator)]
        static IEnumerable<CodeInstruction> ModifyFiringSequence(IEnumerable<CodeInstruction> instructions, ILGenerator il, MethodBase method)
        {
            Resignation.Logger.LogMessage("Enumerator transpile executing");
            Resignation.Logger.LogInfo(gameObjectMethod);
            Resignation.Logger.LogInfo(getComponentMethod);
            
            //FieldInfo abridgedVersionField = AccessTools.Field(method.GetType(), "abridgedVersion");
            Label abridgedControlLabel = il.DefineLabel();
            int ldarg0Count = 0;
            int ldlocCount = 0;    

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.IsLdarg(0))
                {
                    ldarg0Count++;
                    if (ldarg0Count == 4)
                    {
                        Resignation.Logger.LogInfo("INJECTING JUMP CONDITION");
                        yield return new CodeInstruction(OpCodes.Ldloc, 1);
                        yield return new CodeInstruction(OpCodes.Callvirt, gameObjectMethod);
                        //yield return new CodeInstruction(OpCodes.Callvirt, getComponentMethod);
                        yield return new CodeInstruction(OpCodes.Brtrue, abridgedControlLabel);
                    }
                }

                if (instruction.IsLdloc())
                {
                    ldlocCount++;
                    if (ldlocCount == 8)
                    {
                        Resignation.Logger.LogInfo("CONTROL LABEL APPLIED");
                        instruction.labels.Add(abridgedControlLabel);
                    }
                }
                
                yield return instruction;
            }
        }
    }
}
