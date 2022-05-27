// using BrilliantSkies.Core.UiSounds;
using BrilliantSkies.Ftd.Avatar.Input;
using BrilliantSkies.PlayerProfiles;
using HarmonyLib;

namespace FTDCraftControllerCameraMod
{
    [HarmonyPatch(typeof(cInput))]
    public class InputHooks
    {
        /*[HarmonyPatch("NonBuildModePlayerActions")]
        [HarmonyPostfix]*/
        public static void CheckForAirControl()
        {
            if (CameraManager.GetSingleton().CurrentMode == Main.craftCameraMode
                && Main.craftCameraMode.CameraType == EnumCraftCameraType.AIR_DEFAULT
                && ProfileManager.Instance.GetModule<FtdKeyMap>().Bool(
                    KeyInputsFtd.Freeze, KeyInputEventType.Down))
            {
                /*if (Main.airControlSubject != Main.craftCameraMode.Subject)
                    Main.airControlSubject = Main.craftCameraMode.Subject;
                else
                    Main.airControlSubject = null;
                GUISoundManager.GetSingleton().PlayBeep();*/
            }
        }
    }
}
