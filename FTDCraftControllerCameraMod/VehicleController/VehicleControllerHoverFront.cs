using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Control.Pids;
using BrilliantSkies.Ai.Modules;
using BrilliantSkies.Ai.Modules.Behaviour;
using BrilliantSkies.Ai.Modules.Behaviour.Examples;
using BrilliantSkies.Ai.Modules.Behaviour.Examples.Ftd;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvre.Examples;
using BrilliantSkies.Common.Controls.ConstructModules;
using BrilliantSkies.Core.Timing;
using BrilliantSkies.PlayerProfiles;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    /// <summary>
    /// Expect a 6DoF-capable frontsider vehicle.
    /// </summary>
    public class VehicleControllerHoverFront : IVehicleController
    {
        public static readonly float FOCUS_DISTANCE = 500f;
        public static readonly float SQRT_TWO = Mathf.Sqrt(2f);

        private float last_hover_height = 0f;
        private bool last_hover_save = false;

        public void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement, ref float result)
        {
            if (movement is ManoeuvreAbstract ma)
            {
                float gameTime = GameTimer.Instance.TimeCache;
                MainConstruct subject = cameraMode.Subject;

                float current_alt = subject.myTransform.position.y;
                float pitchToThrust = ma.PitchForForward.Us;
                float rollToStrafe = ma.RollForStrafe.Us;
                Vector3 wasd_dir = ProfileManager.Instance.GetModule<FtdKeyMap>().GetMovementDirection(true);

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
                Vector3 sAngles = NormalizeAngles(sTransform.eulerAngles);
                Vector3 dir = Vector3.Normalize(focusPoint - subject.CentreOfMass);
                Quaternion cameraRotation = Quaternion.LookRotation(dir, Vector3.up);
                Quaternion goalRotation = Quaternion.Inverse(sTransform.rotation) * cameraRotation;
                Vector3 goalEula = new Vector3(cameraRotation.eulerAngles.x - sTransform.rotation.eulerAngles.x,
                    goalRotation.eulerAngles.y,
                    -sTransform.rotation.eulerAngles.z)
                {
                    // Vector3 goalEula = goalRotation.eulerAngles;
                    x = cameraRotation.eulerAngles.x - sTransform.rotation.eulerAngles.x,
                    z = -sTransform.rotation.eulerAngles.z // Try to cancel any roll by default.
                };
                goalEula = NormalizeAngles(goalEula);
                goalEula.x += wasd_dir.z * pitchToThrust; // Hover movement pitches for forward/backward movement.
                goalEula.z -= wasd_dir.x * rollToStrafe; // Hover movement rolls for left/right movement.

                // Use AI PID to control.
                float yaw = yawControl.NewMeasurement(goalEula.y, 0f, gameTime);
                float pitch = pitchControl.NewMeasurement(goalEula.x, 0f, gameTime);
                float roll = rollControl.NewMeasurement(goalEula.z, 0f, gameTime);
                // -1 is yaw left, +1 is yaw right
                subject.ControlsRestricted.MakeRequest(ControlType.PitchDown, pitch);
                subject.ControlsRestricted.MakeRequest(ControlType.YawRight, yaw);
                result += yaw;
                subject.ControlsRestricted.MakeRequest(ControlType.RollLeft, roll);

                float forward = wasd_dir.z;
                float hover = 0f;
                float pitchAbs = Mathf.Abs(sAngles.x);
                float pitchRads = sAngles.x * Mathf.Deg2Rad;
                float rollRads = sAngles.z * Mathf.Deg2Rad;
                Vector3 normalVelocity = Quaternion.Inverse(sTransform.rotation) * subject.Velocity;
                // Forward Dampener
                if (wasd_dir.z == 0f)
                    forward = forwardControl.NewMeasurement(0f, Vector3.Project(normalVelocity, Vector3.forward).z, gameTime);
                else
                    hover = wasd_dir.z * Mathf.Sin(pitchRads) * SQRT_TWO; // At 45 degrees pitch, we want hover to kick in at max.
                // Strafe Dampener
                if (wasd_dir.x == 0f)
                    subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight,
                        strafeControl.NewMeasurement(0f, Vector3.Project(normalVelocity, Vector3.right).x, gameTime));
                else
                {
                    hover += wasd_dir.x * Mathf.Sin(rollRads) * SQRT_TWO; // At 45 degrees pitch, we want hover to kick in at max.
                    subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight,
                        wasd_dir.x * Mathf.Cos(rollRads));
                }
                // Hover Automatic Altitude Control
                if (last_hover_save && wasd_dir.y == 0f // TODO: Maybe implement deadzone?
                    && (pitchAbs < 45f || wasd_dir.z == 0f)) // Drastically moving up or down with forward thrust means don't try to correct altitude.
                {
                    float double_length = Mathf.Max(subject.AllBasics.sy, subject.AllBasics.sz);
                    last_hover_height = Mathf.Clamp(last_hover_height, // Don't try to correct over a craft's length worth of altitude.
                        current_alt - double_length, current_alt + double_length);
                    hover += hoverControl.NewMeasurement(last_hover_height, current_alt, gameTime)
                        * Mathf.Cos(pitchRads) * Mathf.Cos(rollRads); // Don't use hover if pitched or rolled over.
                    subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, hover);
                    // Use forward/backward thrust to control altitude when rotated.
                    if (wasd_dir.z == 0f && pitchAbs > pitchToThrust)
                        forward -= hover * Mathf.Sin(pitchRads);
                    // I forgor math signs so I won't bother with strafe altitude control.
                    // Strafe probably wouldn't help anyway.
                    // if (wasd_dir.x == 0f && Mathf.Abs(sAngles.z) > rollToStrafe)
                }
                else
                {
                    subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, wasd_dir.y);
                    last_hover_height = current_alt;
                }
                last_hover_save = true;
                subject.ControlsRestricted.MakeRequest(ControlType.ThrustForward, forward);
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
                case ManoeuvreSixAxis _:
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
