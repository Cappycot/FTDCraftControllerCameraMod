using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Control.Pids;
using BrilliantSkies.Ai.Modules;
using BrilliantSkies.Ai.Modules.Behaviour;
using BrilliantSkies.Ai.Modules.Behaviour.Examples;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Common.Controls.ConstructModules;
using BrilliantSkies.Core.Timing;
using BrilliantSkies.PlayerProfiles;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    /// <summary>
    /// Expect a 6DoF-capable frontsider vehicle.
    /// </summary>
    public class VehicleControllerHelicopter : IVehicleController
    {
        public static readonly float FOCUS_DISTANCE = 500f;
        public static readonly float SQRT_TWO = Mathf.Sqrt(2f);

        private float last_hover_alt = 0f;
        private bool last_hover_save = false;

        public void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster aiMaster, IManoeuvre movement, ref float result)
        {
            if (movement is ManoeuvreAbstract ma)
            {
                float gameTime = GameTimer.Instance.TimeCache;
                MainConstruct subject = cameraMode.Subject;

                float current_alt = subject.CentreOfMass.y;
                float pitchToThrust = ma.PitchForForward.Us;
                float rollToStrafe = ma.RollForStrafe.Us;
                FtdKeyMap key_map = ProfileManager.Instance.GetModule<FtdKeyMap>();
                Vector3 wasd_dir = key_map.GetMovementDirection(false);
                float max_look_pitch = key_map.Bool(KeyInputsFtd.SpeedUpCamera, KeyInputEventType.Held) ? 
                    90f : VehicleUtils.GetMaxPitchFromAiMaster(aiMaster);
                float max_pitch = Mathf.Min(max_look_pitch + pitchToThrust, 90f);

                // TODO: Are these PIDs even okay in multiplayer?
                subject.ControlsRestricted.PlayerControllingNow();
                VariableControllerMaster yawControl = aiMaster.Common.YawControl;
                VariableControllerMaster rollControl = aiMaster.Common.RollControl;
                VariableControllerMaster pitchControl = aiMaster.Common.PitchControl;
                VariableControllerMaster hoverControl = aiMaster.Common.HoverControl;
                VariableControllerMaster strafeControl = aiMaster.Common.StrafeControl;
                VariableControllerMaster forwardControl = aiMaster.Common.ForwardBackwardControl;

                // Get camera focus from transform.
                Transform cTransform = Main.craftCameraMode.Transform;
                Vector3 focusPoint = cTransform.position + FOCUS_DISTANCE * cTransform.forward;

                // Calculate pitch and yaw for vehicle to camera.
                Transform sTransform = subject.myTransform;
                Vector3 sAngles = VehicleUtils.NormalizeAngles(sTransform.eulerAngles);
                Vector3 dir = Vector3.Normalize(focusPoint - subject.CentreOfMass);
                Quaternion cameraRotation = Quaternion.LookRotation(dir, Vector3.up);
                cameraRotation = Quaternion.Euler(Mathf.Clamp(VehicleUtils.NormalizeAngle(cameraRotation.eulerAngles.x), -max_look_pitch, max_look_pitch),
                    cameraRotation.eulerAngles.y, cameraRotation.eulerAngles.z);
                Quaternion goalRotation = Quaternion.Inverse(sTransform.rotation) * cameraRotation;
                Vector3 goalEula = new Vector3(cameraRotation.eulerAngles.x - sAngles.x, goalRotation.eulerAngles.y, -sAngles.z);
                goalEula = VehicleUtils.NormalizeAngles(goalEula);
                goalEula.x += wasd_dir.z * pitchToThrust; // Hover movement pitches for forward/backward movement.
                goalEula.z -= wasd_dir.x * rollToStrafe; // Hover movement rolls for left/right movement.

                // Use AI PID to control.
                float yaw = yawControl.NewMeasurement(goalEula.y + sAngles.y, sAngles.y, gameTime);

                // float pitch = pitchControl.NewMeasurement(goalEula.x + sAngles.x, sAngles.x, gameTime);
                float pitch = pitchControl.NewMeasurement(Mathf.Clamp(goalEula.x + sAngles.x, -max_pitch, max_pitch), sAngles.x, gameTime);

                float roll = rollControl.NewMeasurement(goalEula.z + sAngles.z, sAngles.z, gameTime);
                // -1 is yaw left, +1 is yaw right
                subject.ControlsRestricted.MakeRequest(ControlType.PitchDown, pitch);
                subject.ControlsRestricted.MakeRequest(ControlType.YawRight, yaw);
                result += yaw;
                subject.ControlsRestricted.MakeRequest(ControlType.RollLeft, roll);

                float forward = wasd_dir.z;
                float hover;
                float pitchAbs = Mathf.Abs(sAngles.x);
                float pitchRads = sAngles.x * Mathf.Deg2Rad;
                float rollRads = sAngles.z * Mathf.Deg2Rad;
                // Vector3 normalVelocity = Quaternion.Inverse(sTransform.rotation) * subject.Velocity;
                Vector3 normalVelocity = Quaternion.Inverse(Quaternion.Euler(0f, sAngles.y, 0f)) * subject.Velocity;
                // Forward Vector Dampener
                if (wasd_dir.z == 0f)
                {
                    // forward = forwardControl.NewMeasurement(0f, Vector3.Project(normalVelocity, Vector3.forward).z, gameTime);
                    float forward_cancel = forwardControl.NewMeasurement(0f, Vector3.Project(normalVelocity, Vector3.forward).z, gameTime);
                    forward = forward_cancel * Mathf.Cos(pitchRads);
                    hover = forward_cancel * Mathf.Sin(pitchRads);
                }
                else
                    hover = Mathf.Abs(wasd_dir.z * Mathf.Sin(pitchToThrust * Mathf.Deg2Rad));
                // Strafe Dampener
                if (wasd_dir.x == 0f)
                    subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight,
                        strafeControl.NewMeasurement(0f, Vector3.Project(normalVelocity, Vector3.right).x, gameTime));
                else
                {
                    hover += wasd_dir.x * -Mathf.Sin(rollRads);
                    subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight,
                        wasd_dir.x * Mathf.Cos(rollRads));
                }
                // Hover Automatic Altitude Control
                if (last_hover_save && wasd_dir.y == 0f // TODO: Maybe implement deadzone?
                    && (pitchAbs < 45f || wasd_dir.z == 0f)) // Drastically moving up or down with forward thrust means don't try to correct altitude.
                {
                    float double_length = Mathf.Max(subject.AllBasics.sy, subject.AllBasics.sz);
                    last_hover_alt = Mathf.Clamp(last_hover_alt, // Don't try to correct over a craft's length worth of altitude.
                        current_alt - double_length, current_alt + double_length);

                    // Since hover PID result is used by both hover and forward controls depending on pitch,
                    // use separate variable to add to both axes.
                    float hover_result = hoverControl.NewMeasurement(last_hover_alt, current_alt, gameTime);
                    hover += hover_result * Mathf.Cos(pitchRads) * Mathf.Cos(rollRads); // Don't use hover if pitched or rolled over.
                    subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, hover);
                    // Use forward/backward thrust to control altitude when rotated.
                    if (wasd_dir.z == 0f && pitchAbs > pitchToThrust)
                        forward -= hover_result * Mathf.Sin(pitchRads);

                    // I forgor math signs so I won't bother with strafe altitude control.
                    // Strafe probably wouldn't help anyway since you roll to strafe in the first place.
                    // if (wasd_dir.x == 0f && Mathf.Abs(sAngles.z) > rollToStrafe)
                }
                else
                {
                    subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, wasd_dir.y);
                    last_hover_alt = current_alt;
                }
                last_hover_save = true;
                if (wasd_dir.z == 0f)
                {
                    subject.ControlsRestricted.StopDrive(Drive.Main);
                    subject.ControlsRestricted.MakeRequest(ControlType.ThrustForward, forward);
                }
                else
                    subject.ControlsRestricted.MakeRequest(ControlType.PrimaryRun, forward);
            }
            else
                last_hover_save = false;
        }

        public void Enter()
        {
            Reenter();
        }

        public VehicleMatch GetVehicleMatch(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement)
        {
            switch (movement)
            {
                case ManoeuvreHover _:
                    for (int i = 0; i < master.Pack.Packages.Count; i++)
                    {
                        AiBaseAbstract aiBaseAbstract = master.Pack.Packages[i];
                        if (aiBaseAbstract.RoutineType == AiRoutineType.Behaviour
                            // List of frontsider behaviors.
                            && (aiBaseAbstract is BehaviourCharge
                            || aiBaseAbstract is BehaviourPointAndMaintainDistance
                            || aiBaseAbstract is BehaviourPointAndMaintainDistanceLegacy))
                            return VehicleMatch.DEFAULT;
                    }
                    return VehicleMatch.NO;
                default:
                    return VehicleMatch.NO;
            }
        }

        public void Reenter()
        {
            last_hover_save = false;
        }

        public bool KeyPressed(KeyInputsForVehicles key)
        {
            FtdKeyMap key_map = ProfileManager.Instance.GetModule<FtdKeyMap>();
            switch (key)
            {
                // Throttle
                case KeyInputsForVehicles.AirPrimaryUp:
                case KeyInputsForVehicles.WaterPrimaryUp:
                    return key_map.Bool(KeyInputsFtd.MoveForward, KeyInputEventType.Held);
                case KeyInputsForVehicles.AirPrimaryDown:
                case KeyInputsForVehicles.WaterPrimaryDown:
                    return key_map.Bool(KeyInputsFtd.MoveBackward, KeyInputEventType.Held);
                case KeyInputsForVehicles.AirPrimaryZero:
                case KeyInputsForVehicles.WaterPrimaryZero:
                    return !key_map.Bool(KeyInputsFtd.MoveForward, KeyInputEventType.Held)
                        && !key_map.Bool(KeyInputsFtd.MoveBackward, KeyInputEventType.Held);
                // Hover
                case KeyInputsForVehicles.ComplexUpArrow:
                    return key_map.Bool(KeyInputsFtd.MoveUp, KeyInputEventType.Held);
                case KeyInputsForVehicles.ComplexDownArrow:
                    return key_map.Bool(KeyInputsFtd.MoveDown, KeyInputEventType.Held);
                // Strafe
                case KeyInputsForVehicles.ComplexLeftArrow:
                    return key_map.Bool(KeyInputsFtd.MoveLeft, KeyInputEventType.Held);
                case KeyInputsForVehicles.ComplexRightArrow:
                    return key_map.Bool(KeyInputsFtd.MoveRight, KeyInputEventType.Held);
                default:
                    return false;
            }
        }
    }
}
