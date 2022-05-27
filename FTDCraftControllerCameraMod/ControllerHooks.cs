using BrilliantSkies.Ftd.Avatar.Control;
using BrilliantSkies.PlayerProfiles;
using HarmonyLib;

namespace FTDCraftControllerCameraMod
{
    /// <summary>
    /// Ignore weapon slot scrolling if camera is in craft control mode.
    /// </summary>
    [HarmonyPatch(typeof(cControl))]
    public class ControllerHooks
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
