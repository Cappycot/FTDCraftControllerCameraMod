using BrilliantSkies.Ftd.Avatar;
using BrilliantSkies.Ftd.Avatar.Movement;
using BrilliantSkies.Ftd.Cameras;
using HarmonyLib;

namespace FTDCraftControllerCameraMod
{
    [HarmonyPatch(typeof(cCameraControl))]
    public class CameraModeHooks
    {
        /// <summary>
        /// Hook the craft camera mode into the client camera control.
        /// </summary>
        [HarmonyPatch("RunLateUpdate")]
        [HarmonyPostfix]
        public static void AttachCraftCamera(cCameraControl __instance, I_cMovement_HUD ___iHUD)
        {
            // First perform minimal checks that are done every frame.
            ICameraMode current = CameraManager.GetSingleton().CurrentMode;
            if (current == Main.craftCameraMode)
                Main.craftCameraMode.UpdatePosition();
            else if (__instance.CameraState == enumCameraState.firstPerson)
            {
#pragma warning disable CS0252
                if (current == __instance)
#pragma warning restore CS0252
                {
                    // Check if avatar is in chair.
                    I_world_cMovement i_world_cMovement = ClientInterface.GetInterface().Get_I_world_cMovement();
                    if (i_world_cMovement != null && i_world_cMovement.IsInChair())
                    {
                        MainConstruct subject = i_world_cMovement.TheChair().MainConstruct as MainConstruct;
                        Main.craftCameraMode = Main.craftCameraMode ?? new CraftCameraMode(__instance, ___iHUD, subject);
                        CameraManager.GetSingleton().AddCameraMode(Main.craftCameraMode);
                    }
                    else
                        Main.craftCameraMode = null;
                }
                // else as long as cCameraControl is set to first person mode,
                // regardless of current camera mode is indeed cCameraControl,
                // "memorize" the fact that the craft camera mode was cancelled.
            }
            else if (Main.craftCameraMode != null)
            {
                Main.craftCameraMode.Cancel();
                Main.craftCameraMode = null;
            }
        }

        /// <summary>
        /// Disable the splash noise when the camera is submerged.
        /// </summary>
        [HarmonyPatch("IsFirstPerson", MethodType.Getter)]
        [HarmonyPostfix]
        public static void DenyFirstPerson(ref bool __result)
        {
            __result = __result && CameraManager.GetSingleton().CurrentMode != Main.craftCameraMode;
        }

        /// <summary>
        /// Need the player to still be able to "jump" out of the seat.
        /// </summary>
        [HarmonyPatch("PlayerInControlOfCharacter", MethodType.Getter)]
        [HarmonyPostfix]
        public static void FixPlayerInControlOfCharacter(ref bool __result)
        {
            __result = __result || CameraManager.GetSingleton().CurrentMode == Main.craftCameraMode;
        }

        /// <summary>
        /// Lets the player tab back into normal first person mode.
        /// </summary>
        [HarmonyPatch("ChangeCameraMode")]
        [HarmonyPrefix]
        public static bool CancelCraftCamera()
        {
            if (CameraManager.GetSingleton().CurrentMode == Main.craftCameraMode)
            {
                Main.craftCameraMode.Cancel();
                return false;
            }
            return true;
        }
    }
}
