using GameNetcodeStuff;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Resignation.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]

    internal class StartOfRoundPatch
    {
        private static InteractTrigger OpenDoorTrigger;

        static UnityAction<PlayerControllerB> PrepareResignationEvent;
        static UnityAction<PlayerControllerB> BeginResignationEvent;
        static UnityAction<PlayerControllerB> CancelResignationEvent;

        static Coroutine PrepareResignationCoroutine;
        static bool isAlarmRinging;

        static TextMeshProUGUI HeaderMesh;
        static TextMeshProUGUI SubMesh;

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void AppendResignationTrigger(StartOfRound __instance)
        {
            GameObject maskImage = GameObject.Find("Systems/UI/Canvas/GameOverScreen/MaskImage/");

            GameObject headerText = maskImage.transform.Find("HeaderText").gameObject;
            GameObject subText = maskImage.transform.Find("HeaderText (1)").gameObject;

            HeaderMesh = headerText.GetComponent<TextMeshProUGUI>();
            SubMesh = subText.GetComponent<TextMeshProUGUI>();

            Transform button = GameObject.Find("Environment/HangarShip/AnimatedShipDoor/HangarDoorButtonPanel/StartButton/Cube (2)").transform;
            OpenDoorTrigger = button.GetComponent<InteractTrigger>();

            OpenDoorTrigger.holdInteraction = true;
            OpenDoorTrigger.timeToHold = 5f;

            PrepareResignationEvent += PrepareResignation;
            BeginResignationEvent += BeginResignation;
            CancelResignationEvent += CancelResignation;

            OpenDoorTrigger.onInteractEarly.AddListener(PrepareResignation);
            OpenDoorTrigger.onInteract.AddListener(BeginResignationEvent);
            OpenDoorTrigger.onStopInteract.AddListener(CancelResignationEvent);
        }

        static void BeginResignation(PlayerControllerB controller)
        {
            if (StartOfRound.Instance.inShipPhase)
            {
                ModifyGameOverDisplay();
                StartOfRound.Instance.ManuallyEjectPlayersServerRpc();
            }
        }

        static void PrepareResignation(PlayerControllerB controller)
        {
            PrepareResignationCoroutine = StartOfRound.Instance.StartCoroutine((TriggerAlarm()));
        }

        public static IEnumerator TriggerAlarm()
        {
            yield return new WaitForSeconds(1f);

            StartOfRound startOfRound = StartOfRound.Instance;
            startOfRound.shipAnimatorObject.gameObject.GetComponent<Animator>().SetBool("AlarmRinging", true);
            startOfRound.shipDoorAudioSource.PlayOneShot(startOfRound.alarmSFX);
            isAlarmRinging = true;
        }

        static void CancelResignation(PlayerControllerB controller)
        {
            StartOfRound startOfRound = StartOfRound.Instance;

            if (!startOfRound.suckingPlayersOutOfShip)
            {
                if (PrepareResignationCoroutine != null)
                {
                    startOfRound.StopCoroutine(PrepareResignationCoroutine);
                    if (isAlarmRinging)
                    {
                        startOfRound.shipDoorAudioSource.Stop();
                        startOfRound.speakerAudioSource.PlayOneShot(startOfRound.disableSpeakerSFX);
                        startOfRound.shipAnimatorObject.gameObject.GetComponent<Animator>().SetBool("AlarmRinging", false);
                        isAlarmRinging = false;
                    }
                }
            }
        }

        [HarmonyPatch("openingDoorsSequence", MethodType.Enumerator)]
        [HarmonyPrefix]
        private static void ResetDoorTrigger()
        {
            OpenDoorTrigger.holdInteraction = false;
        }

        [HarmonyPatch("SetShipReadyToLand")]
        [HarmonyPrefix]
        private static void ModifyDoorTrigger()
        {
            OpenDoorTrigger.holdInteraction = true;
        }

        [HarmonyPatch("EndPlayersFiredSequenceClientRpc")]
        [HarmonyPrefix]
        private static void ResetGameOverDisplay()
        {
            HeaderMesh.text = "YOU ARE FIRED.";
            HeaderMesh.fontSize = 80;
            SubMesh.text = "You did not meet the profit quota before the deadline.";
        }

        private static void ModifyGameOverDisplay()
        {
            HeaderMesh.text = "YOU HAVE RESIGNED.";
            HeaderMesh.fontSize = 63;
            SubMesh.text = "You parted ways with The Company on amicable terms.";
        }

        static readonly MethodInfo getTimeOfDay = AccessTools.Method(typeof(TimeOfDay), "get_Instance");
        static readonly FieldInfo timeUntilDeadlineField = AccessTools.Field(typeof(TimeOfDay), nameof(TimeOfDay.timeUntilDeadline));

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(StartOfRound.Instance.playersFiredGameOver), MethodType.Enumerator)]
        static IEnumerable<CodeInstruction> ModifyFiringSequence(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            int ldarg0Count = 0;
            int ldlocCount = 0;
            Label abridgedControlLabel = il.DefineLabel();

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.IsLdarg(0))
                {
                    ldarg0Count++;
                    if (ldarg0Count == 4)
                    {
                        yield return new CodeInstruction(OpCodes.Call, getTimeOfDay);
                        yield return new CodeInstruction(OpCodes.Ldfld, timeUntilDeadlineField);
                        yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
                        yield return new CodeInstruction(OpCodes.Clt);
                        yield return new CodeInstruction(OpCodes.Brfalse, abridgedControlLabel);
                    }
                }

                if (instruction.IsLdloc())
                {
                    ldlocCount++;
                    if (ldlocCount == 8)
                    {
                        instruction.labels.Add(abridgedControlLabel);
                    }
                }
                yield return instruction;
            }
        }
    }
}
