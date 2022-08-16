using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Control.Pids;
using BrilliantSkies.Ai.Modules;
using BrilliantSkies.Ai.Modules.Behaviour;
using BrilliantSkies.Ai.Modules.Behaviour.Examples;
using BrilliantSkies.Ai.Modules.Behaviour.Examples.Ftd;
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

        public void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement, ref float result)
        {
            if (movement is ManoeuvreAbstract ma)
            {
                float gameTime = GameTimer.Instance.TimeCache;
                MainConstruct subject = cameraMode.Subject;

                float current_alt = subject.CentreOfMass.y;
                float pitchToThrust = ma.PitchForForward.Us;
                float rollToStrafe = ma.RollForStrafe.Us;
                Vector3 wasd_dir = ProfileManager.Instance.GetModule<FtdKeyMap>().GetMovementDirection(false);

                // TODO: Are these PIDs even okay in multiplayer?
                subject.ControlsRestricted.PlayerControllingNow();
                VariableControllerMaster yawControl = master.Common.YawControl;
                VariableControllerMaster rollControl = master.Common.RollControl;
                VariableControllerMaster pitchControl = master.Common.PitchControl;
                VariableControllerMaster hoverControl = master.Common.HoverControl;
                VariableControllerMaster strafeControl = master.Common.StrafeControl;
                VariableControllerMaster forwardControl = master.Common.ForwardBackwardControl;

                // Get camera focus from transform.
                Transform cTransform = Main.craftCameraMode.Transform;
                Vector3 focusPoint = cTransform.position + FOCUS_DISTANCE * cTransform.forward;

                // Calculate pitch and yaw for vehicle to camera.
                Transform sTransform = subject.myTransform;
                Vector3 sAngles = VehicleUtils.NormalizeAngles(sTransform.eulerAngles);
                Vector3 dir = Vector3.Normalize(focusPoint - subject.CentreOfMass);
                Quaternion cameraRotation = Quaternion.LookRotation(dir, Vector3.up);
                Quaternion goalRotation = Quaternion.Inverse(sTransform.rotation) * cameraRotation;
                Vector3 goalEula = new Vector3(cameraRotation.eulerAngles.x - sAngles.x, goalRotation.eulerAngles.y, -sAngles.z);
                goalEula = VehicleUtils.NormalizeAngles(goalEula);
                goalEula.x += wasd_dir.z * pitchToThrust; // Hover movement pitches for forward/backward movement.
                goalEula.z -= wasd_dir.x * rollToStrafe; // Hover movement rolls for left/right movement.

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
                    hover = wasd_dir.z * Mathf.Sin(pitchToThrust * Mathf.Deg2Rad) * SQRT_TWO; // At 45 degrees pitch, we want hover to kick in at max.
                // Strafe Dampener
                if (wasd_dir.x == 0f)
                    subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight,
                        strafeControl.NewMeasurement(0f, Vector3.Project(normalVelocity, Vector3.right).x, gameTime));
                else
                {
                    hover += wasd_dir.x * -Mathf.Sin(rollRads) * SQRT_TWO; // At 45 degrees roll, we want hover to kick in at max.
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

                    /*hover += hoverControl.NewMeasurement(last_hover_alt, current_alt, gameTime);
                    subject.ControlsRestricted.MakeRequest(ControlType.HoverUp,
                        hover * Mathf.Cos(pitchRads) * Mathf.Cos(rollRads)); // Don't use hover if pitched or rolled over.
                    // Use forward/backward thrust to control altitude when rotated.
                    if (wasd_dir.z == 0f && pitchAbs > pitchToThrust)
                        forward -= hover * Mathf.Sin(pitchRads);*/

                    float hover_control = hoverControl.NewMeasurement(last_hover_alt, current_alt, gameTime);
                    hover += hover_control * Mathf.Cos(pitchRads) * Mathf.Cos(rollRads); // Don't use hover if pitched or rolled over.
                    subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, hover);
                    // Use forward/backward thrust to control altitude when rotated.
                    if (wasd_dir.z == 0f && pitchAbs > pitchToThrust)
                        forward -= hover_control * Mathf.Sin(pitchRads);

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
                    // ManoeuvreAbstract ma = (ManoeuvreAbstract) movement;
                    for (int i = 0; i < master.Pack.Packages.Count; i++)
                    {
                        AiBaseAbstract aiBaseAbstract = master.Pack.Packages[i];
                        if (aiBaseAbstract.RoutineType == AiRoutineType.Behaviour
                            && (aiBaseAbstract is BehaviourBombingRun
                            || aiBaseAbstract is BehaviourCharge
                            || aiBaseAbstract is BehaviourPointAndMaintainDistance
                            || aiBaseAbstract is BehaviourPointAndMaintainDistanceLegacy
                            || aiBaseAbstract is FtdAerial))
                            return VehicleMatch.DEFAULT;
                    }
                    // return ma.PitchForForward > 0f && ma.RollForStrafe > 0f ? VehicleMatch.DEFAULT : VehicleMatch.NO;
                    return VehicleMatch.NO;
                default:
                    return VehicleMatch.NO;
            }
        }

        public void Reenter()
        {
            last_hover_save = false;
        }
    }
}
