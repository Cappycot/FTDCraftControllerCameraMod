using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvres;
using BrilliantSkies.PlayerProfiles;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    public class VehicleControllerFortress : IVehicleController
    {
        public static readonly float FORTRESS_SPEED = 3f; // TODO: Get actual value via reflections. :(

        public void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement, ref float result)
        {
            MainConstruct subject = cameraMode.Subject;
            Vector3 wasd_dir = ProfileManager.Instance.GetModule<FtdKeyMap>().GetMovementDirection(false);
            float yaw_rad = cameraMode.Transform.eulerAngles.y * Mathf.Deg2Rad;
            subject.Controls.PlayerControllingNow();
            if (wasd_dir.x != 0 || wasd_dir.y != 0 || wasd_dir.z != 0)
            {
                Vector3 direction = new Vector3(
                    wasd_dir.x * Mathf.Cos(yaw_rad) + wasd_dir.z * Mathf.Sin(yaw_rad),
                    wasd_dir.y,
                    wasd_dir.z * Mathf.Cos(yaw_rad) - wasd_dir.x * Mathf.Sin(yaw_rad)
                ).normalized;
                subject.MoveFortress(direction * FORTRESS_SPEED);
            }
        }

        public void Enter() { }

        public VehicleMatch GetVehicleMatch(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement)
        {
            return movement is FortressManoeuvre ? VehicleMatch.DEFAULT : VehicleMatch.NO;
        }

        public void Reenter() { }

        public bool KeyPressed(KeyInputsForVehicles key)
        {
            // TODO: Fortress keys
            return false;
        }
    }
}
