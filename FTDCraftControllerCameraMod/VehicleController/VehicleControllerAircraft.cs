using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Control.Pids;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvre.Examples.Ftd;
using BrilliantSkies.Common.Controls.ConstructModules;
using BrilliantSkies.Core.Timing;
using BrilliantSkies.PlayerProfiles;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    public class VehicleControllerAircraft : IVehicleController
    {
        public static readonly float PITCH_UP_TO = -315f;
        public static readonly float PITCH_DOWN_TO = 45f;
        public static readonly float FOCUS_DISTANCE = 500f;
        public static readonly Quaternion FORWARD_TO_UP = Quaternion.FromToRotation(Vector3.forward, Vector3.up);

        public void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement, ref float result)
        {
            if (movement is ManoeuvreAbstract ma)
            {
                float gameTime = GameTimer.Instance.TimeCache;
                float rollDeadzone = Mathf.Max(1f, ma.BankingTurnAbove.Us);
                MainConstruct subject = cameraMode.Subject;
                Vector3 wasd_dir = ProfileManager.Instance.GetModule<FtdKeyMap>().GetMovementDirection(true);

                // TODO: Are these PIDs even okay in multiplayer?
                subject.ControlsRestricted.PlayerControllingNow();
                VariableControllerMaster yawControl = master.Common.YawControl;
                VariableControllerMaster rollControl = master.Common.RollControl;
                VariableControllerMaster pitchControl = master.Common.PitchControl;

                // Get camera focus from transform.
                Transform cTransform = Main.craftCameraMode.Transform;
                Vector3 focusPoint = cTransform.position + FOCUS_DISTANCE * cTransform.forward;

                // Calculate pitch, yaw, roll for vehicle to camera.
                Transform sTransform = subject.myTransform;
                Vector3 dir = Vector3.Normalize(focusPoint - subject.CentreOfMass);
                Quaternion cameraRotation = Quaternion.LookRotation(dir, Vector3.up);
                Quaternion goalRotation = Quaternion.Inverse(sTransform.rotation) * cameraRotation;
                Quaternion rollRotation = Quaternion.Inverse(sTransform.rotation
                    * FORWARD_TO_UP) * cameraRotation;
                Vector3 goalEula = goalRotation.eulerAngles;
                goalEula = NormalizeAngles(goalEula);

                // Calculate roll to pitch turn.
                float rollLimit = Mathf.Max(Mathf.Abs(Mathf.Sin(Mathf.Deg2Rad * goalEula.y) * 90f),
                    Mathf.Abs(Mathf.Sin(Mathf.Deg2Rad * goalEula.x) * 90f));
                float rollTest = NormalizeAngle(-rollRotation.eulerAngles.y);
                rollTest = Mathf.Clamp(rollTest, -rollLimit, rollLimit);
                if (Mathf.Abs(goalEula.y) > rollDeadzone
                    || Mathf.Abs(goalEula.x) > rollDeadzone) // Needed to complete the pitch to yaw sequence.
                    goalEula.z = rollTest;

                // Use AI PID to control.
                float yaw = yawControl.NewMeasurement(goalEula.y, 0f, gameTime);
                float pitch = pitchControl.NewMeasurement(goalEula.x, 0f, gameTime);
                float roll = rollControl.NewMeasurement(goalEula.z, 0f, gameTime);
                // -1 is yaw left, +1 is yaw right
                subject.ControlsRestricted.MakeRequest(ControlType.PitchDown, pitch);
                subject.ControlsRestricted.MakeRequest(ControlType.YawRight, yaw);
                result += yaw;
                subject.ControlsRestricted.MakeRequest(ControlType.RollLeft, roll);

                // TODO: Temp WASD controls.
                subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight, wasd_dir.x);
                subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, wasd_dir.y);
                subject.ControlsRestricted.MakeRequest(ControlType.PrimaryIncrease, wasd_dir.z);
            }
        }

        public void Enter() { }

        public VehicleMatch GetVehicleMatch(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement)
        {
            switch (movement)
            {
                case ManoeuvreAirplane _:
                case FtdAerialMovement _:
                    return VehicleMatch.DEFAULT;
                default:
                    return VehicleMatch.NO;
            }
        }

        public void Reenter() { }

        // TODO: Maybe move to utils class.
        private static Vector3 NormalizeAngles(Vector3 vector3)
        {
            vector3.x = NormalizeAngle(vector3.x);
            vector3.y = NormalizeAngle(vector3.y);
            vector3.z = NormalizeAngle(vector3.z);
            return vector3;
        }

        // TODO: Move to utils class.
        private static float NormalizeAngle(float angle, float min = -180f, float max = 180f)
        {
            angle %= 360f;
            if (angle < min)
                angle += 360f;
            else if (angle > max)
                angle -= 360f;
            return angle;
        }
    }
}
