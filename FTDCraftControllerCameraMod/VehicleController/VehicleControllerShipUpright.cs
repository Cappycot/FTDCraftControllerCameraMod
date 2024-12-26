using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Control.Pids;
using BrilliantSkies.Ai.Modules;
using BrilliantSkies.Ai.Modules.Behaviour;
using BrilliantSkies.Ai.Modules.Behaviour.Examples;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvre.Examples.Ftd;
using BrilliantSkies.Common.Controls.ConstructModules;
using BrilliantSkies.Core.Timing;
using BrilliantSkies.Core.Widgets;
using BrilliantSkies.Ftd.Terrain;
using BrilliantSkies.PlayerProfiles;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    /// <summary>
    /// Expect an upright vehicle with turreted/broadside weapons.
    /// </summary>
    public class VehicleControllerShipUpright : IVehicleController
    {
        private readonly RealTimeUntil brake_timer = new RealTimeUntil();
        private float last_hover_alt = 0f;
        private float lowest_hover_alt = 0f; // Craft will follow the terrain based on min height above land.
        private bool last_hover_save = false;
        private float last_yaw_dir = 0f;
        private bool last_yaw_save = false;

        public void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement, ref float result)
        {
            // AI Movement Parameters
            bool use_hover = true;
            bool use_pitch = true;
            float goal_pitch = 0f;
            switch (movement)
            {
                case FtdNavalAndLandManoeuvre nl:
                    use_hover = nl.AllowHover.Us;
                    use_pitch = nl.UsePitchControls.Us;
                    goal_pitch = nl.IdealPitch.Us;
                    break;
                default:
                    break;
            }

            float gameTime = GameTimer.Instance.TimeCache;
            MainConstruct subject = cameraMode.Subject;
            Transform sTransform = subject.myTransform;
            Vector3 sPosition = subject.CentreOfMass;
            Vector3 sAngles = VehicleUtils.NormalizeAngles(sTransform.eulerAngles);
            FtdKeyMap key_map = ProfileManager.Instance.GetModule<FtdKeyMap>();
            Vector3 wasd_dir = key_map.GetMovementDirection(false);

            // TODO: Are these PIDs even okay in multiplayer?
            subject.ControlsRestricted.PlayerControllingNow();
            VariableControllerMaster rollControl = master.Common.RollControl;
            VariableControllerMaster pitchControl = master.Common.PitchControl;
            VariableControllerMaster yawControl = master.Common.YawControl;
            VariableControllerMaster hoverControl = master.Common.HoverControl;
            VariableControllerMaster strafeControl = master.Common.StrafeControl;
            VariableControllerMaster forwardControl = master.Common.ForwardBackwardControl;

            float yaw = wasd_dir.x;
            if (yaw == 0f && last_yaw_save)
                // yaw = yawControl.NewMeasurement(last_yaw_dir, sAngles.y, gameTime);
                yaw = yawControl.NewMeasurement(VehicleUtils.NormalizeAngle(last_yaw_dir, sAngles.y - 180f, sAngles.y + 180f), sAngles.y, gameTime);
            else
            {
                last_yaw_dir = sAngles.y;
                last_yaw_save = true;
            }
            subject.ControlsRestricted.MakeRequest(ControlType.YawRight, yaw);
            result += yaw;

            if (use_pitch)
                subject.ControlsRestricted.MakeRequest(ControlType.PitchDown,
                    pitchControl.NewMeasurement(-goal_pitch, sAngles.x, gameTime));
            subject.ControlsRestricted.MakeRequest(ControlType.RollLeft,
                rollControl.NewMeasurement(0f, sAngles.z, gameTime));

            Vector3 normalVelocity = Quaternion.Inverse(sTransform.rotation) * subject.Velocity;

            // Forward WS controls
            if (key_map.Bool(KeyInputsFtd.MoveForward, KeyInputEventType.Held)
                && key_map.Bool(KeyInputsFtd.MoveBackward, KeyInputEventType.Held))
            {
                subject.ControlsRestricted.StopDrive(Drive.Main);
                brake_timer.Now(ConstructableController.brakeTime);
            }
            if (wasd_dir.z != 0f && brake_timer.HappenedOrNeverSet)
                subject.ControlsRestricted.MakeRequest(ControlType.PrimaryIncrease, wasd_dir.z);
            // Attempt to actively dampen forward speed if throttle ordered stop.
            else if (subject.ControlsRestricted.MainSpeed == 0f)
            {
                subject.ControlsRestricted.MakeRequest(ControlType.ThrustForward,
                    forwardControl.NewMeasurement(0f, Vector3.Project(normalVelocity, Vector3.forward).z, gameTime));
            }
            // Dampen strafe if applicable.
            subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight, 
                strafeControl.NewMeasurement(0f, Vector3.Project(normalVelocity, Vector3.right).x, gameTime));

            // Player controls hover iff use_hover is true, wasd_dir.y != 0f and current altitude is within AI adjustments.
            float min_alt = Mathf.Max(StaticTerrainAltitude.AltitudeForGameWorldPositionInMainFrame(sPosition)
                + master.Adjustments.MinimumAltitudeAboveLand,
                master.Adjustments.MinimumAltitudeAboveWater);
            float max_alt = Mathf.Max(min_alt, master.Adjustments.MaximumAltitude);

            last_hover_alt = Mathf.Min(Mathf.Clamp(last_hover_alt, min_alt, max_alt),
                Mathf.Max(lowest_hover_alt, min_alt));
            if (wasd_dir.y < 0f && sPosition.y > min_alt
                || wasd_dir.y > 0f && sPosition.y < max_alt)
            {
                last_hover_alt = lowest_hover_alt = Mathf.Clamp(sPosition.y, min_alt, max_alt);
                subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, wasd_dir.y);
            }
            else if (use_hover && last_hover_save)
                subject.ControlsRestricted.MakeRequest(ControlType.HoverUp,
                    hoverControl.NewMeasurement(last_hover_alt, sPosition.y, gameTime));
            else
                last_hover_alt = lowest_hover_alt = Mathf.Clamp(sPosition.y, min_alt, max_alt);
            last_hover_save = true;
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
                            return VehicleMatch.NO;
                    }
                    return VehicleMatch.DEFAULT;
                case FtdNavalAndLandManoeuvre _:
                    return VehicleMatch.DEFAULT;
                default:
                    return VehicleMatch.NO;
            }
        }

        public void Reenter()
        {
            last_hover_save = false;
            last_yaw_save = false;
        }

        public bool KeyPressed(KeyInputsForVehicles key)
        {
            FtdKeyMap key_map = ProfileManager.Instance.GetModule<FtdKeyMap>();
            switch (key)
            {
                // Yaw
                case KeyInputsForVehicles.AirYawLeft:
                case KeyInputsForVehicles.WaterYawLeft:
                    return key_map.Bool(KeyInputsFtd.MoveLeft, KeyInputEventType.Held);
                case KeyInputsForVehicles.AirYawRight:
                case KeyInputsForVehicles.WaterYawRight:
                    return key_map.Bool(KeyInputsFtd.MoveRight, KeyInputEventType.Held);
                // Throttle
                case KeyInputsForVehicles.AirPrimaryUp:
                case KeyInputsForVehicles.WaterPrimaryUp:
                    return key_map.Bool(KeyInputsFtd.MoveForward, KeyInputEventType.Held);
                case KeyInputsForVehicles.AirPrimaryDown:
                case KeyInputsForVehicles.WaterPrimaryDown:
                    return key_map.Bool(KeyInputsFtd.MoveBackward, KeyInputEventType.Held);
                case KeyInputsForVehicles.AirPrimaryZero:
                case KeyInputsForVehicles.WaterPrimaryZero:
                    return key_map.Bool(KeyInputsFtd.MoveForward, KeyInputEventType.Held)
                        && key_map.Bool(KeyInputsFtd.MoveBackward, KeyInputEventType.Held);
                // Hover
                case KeyInputsForVehicles.ComplexUpArrow:
                    return key_map.Bool(KeyInputsFtd.MoveUp, KeyInputEventType.Held);
                case KeyInputsForVehicles.ComplexDownArrow:
                    return key_map.Bool(KeyInputsFtd.MoveDown, KeyInputEventType.Held);
                default:
                    return false;
            }
        }
    }
}