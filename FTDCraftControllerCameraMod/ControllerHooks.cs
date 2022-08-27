using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ftd.Avatar.Control;
using BrilliantSkies.PlayerProfiles;
using HarmonyLib;

namespace FTDCraftControllerCameraMod
{
    [HarmonyPatch(typeof(ConstructableController))]
    public class ControllerHooks
    {
        [HarmonyPatch("WaterSimpleStuff")]
        [HarmonyPostfix]
        public static void CameraWaterControl(ConstructableController __instance, ref float __result)
        {
            CraftControl(__instance, ref __result);
        }

        [HarmonyPatch("AirStuff")]
        [HarmonyPostfix]
        public static void CameraAirControl(ConstructableController __instance, ref float __result)
        {
            CraftControl(__instance, ref __result);
        }

        public static void CraftControl(ConstructableController constructableController, ref float result)
        {
            CraftCameraMode ccm = Main.craftCameraMode;
            if (CameraManager.GetSingleton().CamControl.CurrentMode == ccm
                && ccm.Subject == constructableController.MainConstruct)
            {
                AiMaster master = VehicleUtils.GetMovementAiFromMainConstruct(ccm.Subject, out IManoeuvre movement);
                if (movement == null)
                    return;
                Main.craftCameraMode.vehicleController?.ControlVehicle(ccm, constructableController, master, movement, ref result);
            }
        }
    }

    [HarmonyPatch(typeof(ThrustController))]
    public class ThrustControllerHooks
    {
        [HarmonyPatch("InterpretTheseInputs")]
        [HarmonyPostfix]
        public static void CameraAirControl(ThrustController __instance)
        {
            float f = 0f;
            ControllerHooks.CraftControl(__instance, ref f);
        }
    }

    // I have no clue how this works.
    [HarmonyPatch(typeof(KeyMap<KeyInputsForVehicles>))]
    public class VehicleKeyMapHooks
    {
        [HarmonyPatch("Bool")]
        [HarmonyPostfix]
        public static void SimulateInput(KeyInputsForVehicles id, ref bool __result)
        {
            CraftCameraMode ccm = Main.craftCameraMode;
            IVehicleController vc = ccm?.vehicleController;
            if (CameraManager.GetSingleton().CamControl.CurrentMode == ccm && vc != null)
                __result |= vc.KeyPressed(id);
        }
    }

    /// <summary>
    /// Ignore weapon slot scrolling if camera is in craft control mode.
    /// </summary>
    [HarmonyPatch(typeof(cControl))]
    public class ClientControlHooks
    {
        [HarmonyPatch("IncreaseWeaponSlot")]
        [HarmonyPrefix]
        public static bool BlockIncreaseWeaponSlot()
        {
            FtdKeyMap keyMap = ProfileManager.Instance.GetModule<FtdKeyMap>();
            // Allow if the key being used is not the zoom axis or if we are not in craft camera.
            return !keyMap.IsLastBindingSame(KeyInputsFtd.WeaponNext, KeyInputsFtd.ZoomIn)
                || CameraManager.GetSingleton().CamControl.CurrentMode != Main.craftCameraMode;
        }

        [HarmonyPatch("DecreaseWeaponSlot")]
        [HarmonyPrefix]
        public static bool BlockDecreaseWeaponSlot()
        {
            FtdKeyMap keyMap = ProfileManager.Instance.GetModule<FtdKeyMap>();
            return !keyMap.IsLastBindingSame(KeyInputsFtd.WeaponPrevious, KeyInputsFtd.ZoomOut)
                || CameraManager.GetSingleton().CamControl.CurrentMode != Main.craftCameraMode;
        }
    }
}
