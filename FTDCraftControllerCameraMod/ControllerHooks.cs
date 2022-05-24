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
            return keyMap.Bool(KeyInputsFtd.WeaponNext, KeyInputEventType.Down)
                && !keyMap.IsLastBindingSame(KeyInputsFtd.WeaponNext, KeyInputsFtd.ZoomIn)
                || CameraManager.GetSingleton().CurrentMode != Main.craftCameraMode;
        }

        [HarmonyPatch("DecreaseWeaponSlot")]
        [HarmonyPrefix]
        public static bool BlockDecreaseWeaponSlot()
        {
            FtdKeyMap keyMap = ProfileManager.Instance.GetModule<FtdKeyMap>();
            return keyMap.Bool(KeyInputsFtd.WeaponPrevious, KeyInputEventType.Down)
                && !keyMap.IsLastBindingSame(KeyInputsFtd.WeaponPrevious, KeyInputsFtd.ZoomOut)
                || CameraManager.GetSingleton().CurrentMode != Main.craftCameraMode;
        }
    }
}
