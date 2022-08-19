using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Control.Pids;
using BrilliantSkies.Ai.Modules.Behaviour;
using BrilliantSkies.Ai.Modules.Behaviour.Examples;
using BrilliantSkies.Ai.Modules.Behaviour.Examples.Ftd;
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
        public static readonly float ROLL_REQUIRED = 45f;
        public static readonly Quaternion FORWARD_TO_UP = Quaternion.FromToRotation(Vector3.forward, Vector3.up);

        public void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement, ref float result)
        {
            if (movement is ManoeuvreAbstract ma)
            {
                float gameTime = GameTimer.Instance.TimeCache;
                float rollDeadzone = Mathf.Max(1f, ma.BankingTurnAbove.Us);
                MainConstruct subject = cameraMode.Subject;
                Vector3 wasd_dir = ProfileManager.Instance.GetModule<FtdKeyMap>().GetMovementDirection(false);

                // TODO: Are these PIDs even okay in multiplayer?
                subject.ControlsRestricted.PlayerControllingNow();
                VariableControllerMaster yawControl = master.Common.YawControl;
                VariableControllerMaster rollControl = master.Common.RollControl;
                VariableControllerMaster pitchControl = master.Common.PitchControl;

                // Get camera focus from transform.
                Transform cTransform = Main.craftCameraMode.Transform;
                // Vector3 focusPoint = cTransform.position + FOCUS_DISTANCE * cTransform.forward;
                Vector3 focusPoint = cTransform.position + ma.WanderDistance.Us * cTransform.forward;

                // Calculate pitch, yaw, roll for vehicle to camera.
                Transform sTransform = subject.myTransform;
                Vector3 sAngles = VehicleUtils.NormalizeAngles(sTransform.eulerAngles);
                Vector3 dir = Vector3.Normalize(focusPoint - subject.CentreOfMass);
                Quaternion cameraRotation = Quaternion.LookRotation(dir, Vector3.up);
                Quaternion goalRotation = Quaternion.Inverse(sTransform.rotation) * cameraRotation;
                Quaternion rollRotation = Quaternion.Inverse(sTransform.rotation
                    * FORWARD_TO_UP) * cameraRotation;
                Vector3 goalEula = goalRotation.eulerAngles;
                // goalEula.x = VehicleUtils.NormalizeAngle(goalEula.x, PITCH_UP_TO, PITCH_DOWN_TO);
                float alt_pitch = ma is ManoeuvreAirplane ? ma.PitchForAltitude.Us : 45f;
                goalEula.x = VehicleUtils.NormalizeAngle(goalEula.x, alt_pitch - 360f, alt_pitch);
                goalEula.y = VehicleUtils.NormalizeAngle(goalEula.y);
                goalEula.z = VehicleUtils.NormalizeAngle(goalEula.z);

                // Calculate roll to pitch turn.
                float rollLimit = Mathf.Max(Mathf.Abs(Mathf.Sin(Mathf.Deg2Rad * goalEula.y) * 90f),
                    Mathf.Abs(Mathf.Sin(Mathf.Deg2Rad * goalEula.x) * 90f));
                float rollTest = VehicleUtils.NormalizeAngle(-rollRotation.eulerAngles.y);
                rollTest = Mathf.Clamp(rollTest, -rollLimit, rollLimit);
                if (Mathf.Abs(goalEula.y) > rollDeadzone
                    || Mathf.Abs(goalEula.x) > rollDeadzone) // Needed to complete the pitch to yaw sequence.
                    goalEula.z = rollTest;

                // Use AI PID to control.
                // float yaw = yawControl.NewMeasurement(goalEula.y, 0f, gameTime);
                float yaw = yawControl.NewMeasurement(goalEula.y + sAngles.y, sAngles.y, gameTime);
                // float pitch = pitchControl.NewMeasurement(goalEula.x, 0f, gameTime);
                float pitch = pitchControl.NewMeasurement(goalEula.x + sAngles.x, sAngles.x, gameTime);
                // float roll = rollControl.NewMeasurement(goalEula.z, 0f, gameTime);
                float roll = rollControl.NewMeasurement(goalEula.z + sAngles.z, sAngles.z, gameTime);
                // -1 is yaw left, +1 is yaw right
                subject.ControlsRestricted.MakeRequest(ControlType.PitchDown, pitch);
                subject.ControlsRestricted.MakeRequest(ControlType.YawRight, yaw);
                result += yaw;
                subject.ControlsRestricted.MakeRequest(ControlType.RollLeft, roll);

                subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight, wasd_dir.x);
                subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, wasd_dir.y);
                subject.ControlsRestricted.MakeRequest(ControlType.PrimaryIncrease, wasd_dir.z);
            }
        }

        public void Enter() { }

        public VehicleMatch GetVehicleMatch(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster aiMaster, IManoeuvre movement)
        {
            float turn_roll;
            switch (movement)
            {
                case ManoeuvreAirplane ma:
                    turn_roll = ma.BankingTurnRoll.Us;
                    break;
                case FtdAerialMovement fam:
                    turn_roll = fam.RollToExtremeAngle.Us;
                    break;
                default:
                    return VehicleMatch.NO;
            }
            aiMaster.Pack.GetSelectedBehaviour(out IBehaviour behavior);
            switch (behavior)
            {
                case BehaviourCharge _:
                case BehaviourBombingRun _:
                case FtdAerial _:
                    return turn_roll >= ROLL_REQUIRED ? VehicleMatch.DEFAULT : VehicleMatch.NO;
                default:
                    return VehicleMatch.NO;
            }
        }

        public void Reenter() { }
    }
}
