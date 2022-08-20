using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvres;
using BrilliantSkies.PlayerProfiles;
using System.Reflection;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    public class VehicleControllerFortress : IVehicleController
    {
        public void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement, ref float result)
        {
            if (constructableController is FortressController fortressController)
            {
                float fortress_speed = (float)(typeof(FortressController).GetField("_speed", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(fortressController) ?? 3f);
                MainConstruct subject = cameraMode.Subject;
                FtdKeyMap key_map = ProfileManager.Instance.GetModule<FtdKeyMap>();
                Vector3 wasd_dir = key_map.GetMovementDirection(false);
                bool strafe_mode = key_map.Bool(KeyInputsFtd.SpeedUpCamera, KeyInputEventType.Held);
                float yaw_rad = subject.myTransform.eulerAngles.y * Mathf.Deg2Rad;
                subject.Controls.PlayerControllingNow();
                if (strafe_mode && wasd_dir.x != 0 || wasd_dir.y != 0 || wasd_dir.z != 0)
                {
                    float x_dir = strafe_mode ? wasd_dir.x : 0f;
                    Vector3 direction = new Vector3(
                        x_dir * Mathf.Cos(yaw_rad) + wasd_dir.z * Mathf.Sin(yaw_rad),
                        wasd_dir.y,
                        wasd_dir.z * Mathf.Cos(yaw_rad) - x_dir * Mathf.Sin(yaw_rad)
                    ).normalized;
                    subject.MoveFortress(direction * fortress_speed);
                }
                if (!strafe_mode && wasd_dir.x != 0)
                    subject.SpinFortress(Mathf.Sign(wasd_dir.x));
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
            FtdKeyMap key_map = ProfileManager.Instance.GetModule<FtdKeyMap>();
            bool strafe_mode = key_map.Bool(KeyInputsFtd.SpeedUpCamera, KeyInputEventType.Held);
            switch (key)
            {
                // Yaw
                case KeyInputsForVehicles.WaterYawLeft:
                    return !strafe_mode && key_map.Bool(KeyInputsFtd.MoveLeft, KeyInputEventType.Held);
                case KeyInputsForVehicles.WaterYawRight:
                    return !strafe_mode && key_map.Bool(KeyInputsFtd.MoveRight, KeyInputEventType.Held);
                // Strafe
                case KeyInputsForVehicles.WaterRollLeft:
                    return strafe_mode && key_map.Bool(KeyInputsFtd.MoveLeft, KeyInputEventType.Held);
                case KeyInputsForVehicles.WaterRollRight:
                    return strafe_mode && key_map.Bool(KeyInputsFtd.MoveRight, KeyInputEventType.Held);
                // Hover
                case KeyInputsForVehicles.WaterPitchUp:
                    return key_map.Bool(KeyInputsFtd.MoveDown, KeyInputEventType.Held);
                case KeyInputsForVehicles.WaterPitchDown:
                    return key_map.Bool(KeyInputsFtd.MoveUp, KeyInputEventType.Held);
                // Throttle
                case KeyInputsForVehicles.WaterPrimaryUp:
                    return key_map.Bool(KeyInputsFtd.MoveForward, KeyInputEventType.Held);
                case KeyInputsForVehicles.WaterPrimaryDown:
                    return key_map.Bool(KeyInputsFtd.MoveBackward, KeyInputEventType.Held);
                default:
                    return false;
            }
        }
    }
}
