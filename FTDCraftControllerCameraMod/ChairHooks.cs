using BrilliantSkies.PlayerProfiles;
using HarmonyLib;

namespace FTDCraftControllerCameraMod
{
    /// <summary>
    /// Ignore chair switching if keybind is same and camera is in craft control mode.
    /// </summary>
    [HarmonyPatch(typeof(Chair))]
    public class ChairHooks
    {
        [HarmonyPatch("WarpToChair")]
        [HarmonyPrefix]
        public static bool BlockWarpToChair(bool IsWarpToNextChair)
        {
            FtdKeyMap keyMap = ProfileManager.Instance.GetModule<FtdKeyMap>();
            // Allow if the key being used is not the weapon switch key or if we are not in craft camera.
            return IsWarpToNextChair && !keyMap.IsLastBindingSame(KeyInputsFtd.WeaponNext, KeyInputsFtd.NextVehicle)
                || !IsWarpToNextChair && !keyMap.IsLastBindingSame(KeyInputsFtd.WeaponPrevious, KeyInputsFtd.PreviousVehicle)
                || CameraManager.GetSingleton().CurrentMode != Main.craftCameraMode;
        }
    }
}
