using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Control.Pids;
using BrilliantSkies.Ai.Modules.Behaviour;
using BrilliantSkies.Ai.Modules.Behaviour.Examples;
using BrilliantSkies.Ai.Modules.Behaviour.Examples.Ftd;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvre.Examples.Ftd;
using BrilliantSkies.Common.Controls.ConstructModules;
using BrilliantSkies.Core.Timing;
using BrilliantSkies.Core.Widgets;
using BrilliantSkies.PlayerProfiles;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    public class VehicleControllerAircraft : IVehicleController
    {
        public static readonly float PITCH_REQUIRED = 45f;
        public static readonly float ROLL_REQUIRED = 45f;
        public static readonly Quaternion FORWARD_TO_UP = Quaternion.FromToRotation(Vector3.forward, Vector3.up);

        private readonly RealTimeUntil brake_timer = new RealTimeUntil();

        // PSM Mode
        private Vector3 last_dir = Vector3.zero;
        private float last_roll = 0f;
        private bool last_rotation = false;

        public void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement, ref float result)
        {
            if (movement is ManoeuvreAbstract ma)
            {
                float gameTime = GameTimer.Instance.TimeCache;
                float rollDeadzone = Mathf.Max(1f, ma.BankingTurnAbove.Us);
                MainConstruct subject = cameraMode.Subject;
                FtdKeyMap key_map = ProfileManager.Instance.GetModule<FtdKeyMap>();
                Vector3 wasd_dir = key_map.GetMovementDirection(false);

                // TODO: Are these PIDs even okay in multiplayer?
                subject.ControlsRestricted.PlayerControllingNow();
                VariableControllerMaster yawControl = master.Common.YawControl;
                VariableControllerMaster rollControl = master.Common.RollControl;
                VariableControllerMaster pitchControl = master.Common.PitchControl;

                Transform sTransform = subject.myTransform;
                Vector3 sAngles = VehicleUtils.NormalizeAngles(sTransform.eulerAngles);

                float pitch, yaw, roll;
                if (key_map.Bool(KeyInputsFtd.SpeedUpCamera, KeyInputEventType.Held))
                {
                    // Manual Mode
                    if (wasd_dir.x != 0 || wasd_dir.z != 0 || !last_rotation)
                    {
                        last_dir = sTransform.forward;
                        last_roll = sAngles.z;
                        pitch = wasd_dir.z;
                        yaw = 0f;
                        roll = -wasd_dir.x;
                    }
                    else
                    {
                        Quaternion dirRotation = Quaternion.LookRotation(last_dir, Vector3.up);
                        Quaternion goalRotation = Quaternion.Inverse(sTransform.rotation) * dirRotation;
                        Vector3 goalEula = VehicleUtils.NormalizeAngles(goalRotation.eulerAngles);
                        yaw = yawControl.NewMeasurement(goalEula.y + sAngles.y, sAngles.y, gameTime);
                        pitch = pitchControl.NewMeasurement(goalEula.x + sAngles.x, sAngles.x, gameTime);
                        roll = rollControl.NewMeasurement(VehicleUtils.NormalizeAngle(last_roll, sAngles.z - 180f, sAngles.z + 180f), sAngles.z, gameTime);
                    }
                    last_rotation = true;
                }
                else
                {
                    // Automatic Mode
                    last_rotation = false;
                    // Get camera focus from transform.
                    Transform cTransform = Main.craftCameraMode.Transform;
                    // Vector3 focusPoint = cTransform.position + FOCUS_DISTANCE * cTransform.forward;
                    Vector3 focusPoint = cTransform.position + ma.WanderDistance.Us * cTransform.forward;

                    // Calculate pitch, yaw, roll for vehicle to camera.
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
                    yaw = yawControl.NewMeasurement(goalEula.y + sAngles.y, sAngles.y, gameTime);
                    // float pitch = pitchControl.NewMeasurement(goalEula.x, 0f, gameTime);
                    pitch = pitchControl.NewMeasurement(goalEula.x + sAngles.x, sAngles.x, gameTime);
                    // float roll = rollControl.NewMeasurement(goalEula.z, 0f, gameTime);
                    roll = rollControl.NewMeasurement(goalEula.z + sAngles.z, sAngles.z, gameTime);

                    subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight, wasd_dir.x);

                    // Forward WS controls
                    if (key_map.Bool(KeyInputsFtd.MoveForward, KeyInputEventType.Held)
                    && key_map.Bool(KeyInputsFtd.MoveBackward, KeyInputEventType.Held))
                    {
                        subject.ControlsRestricted.StopDrive(Drive.Main);
                        brake_timer.Now(ConstructableController.brakeTime);
                    }
                    else if (brake_timer.Happened)
                        subject.ControlsRestricted.MakeRequest(ControlType.PrimaryIncrease, wasd_dir.z);
                }

                subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, wasd_dir.y);
                // -1 is yaw left, +1 is yaw right
                subject.ControlsRestricted.MakeRequest(ControlType.PitchDown, pitch);
                subject.ControlsRestricted.MakeRequest(ControlType.YawRight, yaw);
                result += yaw;
                subject.ControlsRestricted.MakeRequest(ControlType.RollLeft, roll);
            }
        }

        public void Enter()
        {
            Reenter();
        }

        public VehicleMatch GetVehicleMatch(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster aiMaster, IManoeuvre movement)
        {
            float pitch_alt;
            float turn_roll;
            switch (movement)
            {
                case ManoeuvreAirplane ma:
                    pitch_alt = ma.PitchForAltitude.Us;
                    turn_roll = ma.BankingTurnRoll.Us;
                    break;
                case FtdAerialMovement fam:
                    pitch_alt = 90f;
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
                    return pitch_alt >= PITCH_REQUIRED && turn_roll >= ROLL_REQUIRED
                        ? VehicleMatch.DEFAULT : VehicleMatch.NO;
                default:
                    return VehicleMatch.NO;
            }
        }

        public void Reenter()
        {
            last_rotation = false;
        }

        public bool KeyPressed(KeyInputsForVehicles key)
        {
            FtdKeyMap key_map = ProfileManager.Instance.GetModule<FtdKeyMap>();
            bool psm_mode = key_map.Bool(KeyInputsFtd.SpeedUpCamera, KeyInputEventType.Held);
            switch (key)
            {
                // Roll
                case KeyInputsForVehicles.AirRollLeft:
                case KeyInputsForVehicles.WaterRollLeft:
                    return psm_mode && key_map.Bool(KeyInputsFtd.MoveLeft, KeyInputEventType.Held);
                case KeyInputsForVehicles.AirRollRight:
                case KeyInputsForVehicles.WaterRollRight:
                    return psm_mode && key_map.Bool(KeyInputsFtd.MoveRight, KeyInputEventType.Held);
                // Pitch
                case KeyInputsForVehicles.AirPitchUp:
                case KeyInputsForVehicles.WaterPitchUp:
                    return psm_mode && key_map.Bool(KeyInputsFtd.MoveBackward, KeyInputEventType.Held);
                case KeyInputsForVehicles.AirPitchDown:
                case KeyInputsForVehicles.WaterPitchDown:
                    return psm_mode && key_map.Bool(KeyInputsFtd.MoveForward, KeyInputEventType.Held);
                // Throttle
                case KeyInputsForVehicles.AirPrimaryUp:
                case KeyInputsForVehicles.WaterPrimaryUp:
                    return !psm_mode && key_map.Bool(KeyInputsFtd.MoveForward, KeyInputEventType.Held);
                case KeyInputsForVehicles.AirPrimaryDown:
                case KeyInputsForVehicles.WaterPrimaryDown:
                    return !psm_mode && key_map.Bool(KeyInputsFtd.MoveBackward, KeyInputEventType.Held);
                case KeyInputsForVehicles.AirPrimaryZero:
                case KeyInputsForVehicles.WaterPrimaryZero:
                    return !psm_mode
                        && key_map.Bool(KeyInputsFtd.MoveForward, KeyInputEventType.Held)
                        && key_map.Bool(KeyInputsFtd.MoveBackward, KeyInputEventType.Held);
                // Hover
                case KeyInputsForVehicles.ComplexUpArrow:
                    return key_map.Bool(KeyInputsFtd.MoveUp, KeyInputEventType.Held);
                case KeyInputsForVehicles.ComplexDownArrow:
                    return key_map.Bool(KeyInputsFtd.MoveDown, KeyInputEventType.Held);
                // Strafe
                case KeyInputsForVehicles.ComplexLeftArrow:
                    return !psm_mode && key_map.Bool(KeyInputsFtd.MoveLeft, KeyInputEventType.Held);
                case KeyInputsForVehicles.ComplexRightArrow:
                    return !psm_mode && key_map.Bool(KeyInputsFtd.MoveRight, KeyInputEventType.Held);
                default:
                    return false;
            }
        }
    }
}
