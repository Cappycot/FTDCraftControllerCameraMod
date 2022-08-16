using BrilliantSkies.Ftd.Avatar;
using BrilliantSkies.Ftd.Avatar.Items;
using BrilliantSkies.PlayerProfiles;
using HarmonyLib;

namespace FTDCraftControllerCameraMod
{
    [HarmonyPatch(typeof(cItem))]
    public class ItemHooks
    {
        [HarmonyPatch("LeftClick")]
        [HarmonyPrefix]
        public static bool BlockLeftClick(bool b)
        {
            return !b || CameraManager.GetSingleton().CurrentMode != Main.craftCameraMode;
        }

        [HarmonyPatch("RightClick")]
        [HarmonyPrefix]
        public static bool BlockRightClick(bool b)
        {
            return !b || CameraManager.GetSingleton().CurrentMode != Main.craftCameraMode;
        }

        [HarmonyPatch("LeftClickDown")]
        [HarmonyPrefix]
        public static bool BlockLeftClickDown()
        {
            return CameraManager.GetSingleton().CurrentMode != Main.craftCameraMode;
        }

        [HarmonyPatch("RightClickDown")]
        [HarmonyPrefix]
        public static bool BlockRightClickDown()
        {
            return CameraManager.GetSingleton().CurrentMode != Main.craftCameraMode;
        }

        /// <summary>
        /// Redirect (default) key 0-9 to weapon slots.
        /// </summary>
        [HarmonyPatch("ChangeItemSlot")]
        [HarmonyPrefix]
        public static bool RedirectItemSlotToWeaponSlot(int i)
        {
            if (CameraManager.GetSingleton().CurrentMode == Main.craftCameraMode)
            {
                // i is 0-9 based on left to right number keys
                // Weapon Slot Key:
                // -1: Control None
                //  0: Control All
                //  N: Control Slot N
                int max_slot = ProfileManager.Instance.GetModule<MGameplay_Ftd>().
                    WeaponSlotOption == WeaponSlotOptions.None5All ? 6 : 1;
                i = i >= max_slot ? -1 : ((i + 1) % max_slot);
                ClientInterface.GetInterface().Get_I_world_cControl()?.SetWeaponSlot(i);
                return false;
            }
            return true;
        }
    }
}
