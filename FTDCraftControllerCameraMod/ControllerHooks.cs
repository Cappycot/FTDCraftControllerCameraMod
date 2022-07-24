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
        [HarmonyPatch("AirStuff")]
        [HarmonyPostfix]
        public static void CameraAirControl(ConstructableController __instance, ref float __result)
        {
            CraftCameraMode ccm = Main.craftCameraMode;
            if (CameraManager.GetSingleton().CurrentMode == Main.craftCameraMode
                && ccm.Subject == __instance.MainConstruct)
            {
                // TODO: Fix so movement of HIGHEST PRIORITY AI is selected.
                BlockStore<AIMainframe> ais = ccm.Subject.iBlockTypeStorage.MainframeStore;
                AiMaster master = null;
                IManoeuvre movement = null;
                for (int i = 0; i < ais.Count; i++)
                    if ((master = ais.Blocks[i].Node.Master).Pack.GetSelectedManoeuvre(out movement))
                        break;
                if (movement == null)
                    return;
                Main.craftCameraMode.vehicleController?.ControlVehicle(ccm, __instance, master, movement, ref __result);
            }
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
                || CameraManager.GetSingleton().CurrentMode != Main.craftCameraMode;
        }

        [HarmonyPatch("DecreaseWeaponSlot")]
        [HarmonyPrefix]
        public static bool BlockDecreaseWeaponSlot()
        {
            FtdKeyMap keyMap = ProfileManager.Instance.GetModule<FtdKeyMap>();
            return !keyMap.IsLastBindingSame(KeyInputsFtd.WeaponPrevious, KeyInputsFtd.ZoomOut)
                || CameraManager.GetSingleton().CurrentMode != Main.craftCameraMode;
        }
    }
}
