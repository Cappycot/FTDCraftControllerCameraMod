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
using BrilliantSkies.Ftd.Terrain;
using BrilliantSkies.PlayerProfiles;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    public class VehicleControllerAircraftUpright : IVehicleController
    {
        private readonly RealTimeUntil brake_timer = new RealTimeUntil();
        private float last_hover_alt = 0f;
        private float lowest_hover_alt = 0f; // Craft will follow the terrain based on min height above land.
        private bool last_hover_save = false;
        private float last_yaw_dir = 0f;
        private bool last_yaw_save = false;

        public void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement, ref float result)
        {
            MainConstruct subject = cameraMode.Subject;
            Transform sTransform = subject.myTransform;
            Vector3 sAngles = VehicleUtils.NormalizeAngles(sTransform.eulerAngles);

            float pitch_for_alt = 45f;
            bool hover_for_pitch = false;
            FtdKeyMap key_map = ProfileManager.Instance.GetModule<FtdKeyMap>();
            Vector3 wasd_dir = key_map.GetMovementDirection(false);
            float pitch_dir = -wasd_dir.y * pitch_for_alt;
            float yaw_dir = wasd_dir.x * 90f;
            float roll_goal = 0f;
            float roll_max;
            float wander_distance;

            float min_alt = Mathf.Max(StaticTerrainAltitude.AltitudeForGameWorldPositionInMainFrame(subject.CentreOfMass)
                + master.Adjustments.MinimumAltitudeAboveLand,
                master.Adjustments.MinimumAltitudeAboveWater);
            float max_alt = Mathf.Max(min_alt, master.Adjustments.MaximumAltitude);

            bool cam_mode = key_map.Bool(KeyInputsFtd.SpeedUpCamera, KeyInputEventType.Held);
            if (cam_mode && movement is ManoeuvreAbstract mw)
            {
                // Get camera focus from transform.
                Transform cTransform = Main.craftCameraMode.Transform;
                // Vector3 focusPoint = cTransform.position + FOCUS_DISTANCE * cTransform.forward;
                Vector3 focusPoint = cTransform.position + mw.WanderDistance.Us * cTransform.forward;
                focusPoint.y = Mathf.Clamp(focusPoint.y, min_alt, max_alt);

                // Calculate pitch, yaw, roll for vehicle to camera.
                Vector3 dir = Vector3.Normalize(focusPoint - subject.CentreOfMass);
                Quaternion cameraRotation = Quaternion.LookRotation(dir, Vector3.up);
                pitch_dir = VehicleUtils.NormalizeAngle(cameraRotation.eulerAngles.x);
                last_yaw_dir = VehicleUtils.NormalizeAngle(cameraRotation.eulerAngles.y);
                last_yaw_save = true;
                last_hover_save = false;
            }

            if (last_yaw_save && wasd_dir.x == 0f || cam_mode)
                yaw_dir = VehicleUtils.NormalizeAngle(last_yaw_dir - VehicleUtils.NormalizeAngle(sAngles.y));
            else
                last_yaw_dir = VehicleUtils.NormalizeAngle(sAngles.y);

            // Calculate roll
            if (movement is ManoeuvreAirplane ma)
            {
                hover_for_pitch = ma.PitchTypeToUse.Us == PitchType.ForAltitude;
                pitch_for_alt = ma.PitchForAltitude.Us;
                wander_distance = ma.WanderDistance.Us;
                roll_max = ma.BankingTurnRoll.Us;
                float roll_min = ma.BankingTurnAbove.Us;
                float roll_range = ma.BankingTurnRollRange.Us;
                if (Mathf.Abs(yaw_dir) > roll_min)
                {
                    float roll_mag = roll_range > 0
                        ? Mathf.InverseLerp(roll_min, roll_min + roll_range, Mathf.Abs(yaw_dir)) : 1f;
                    roll_goal = -Mathf.Sign(yaw_dir) * roll_mag * roll_max;
                }
            }
            else if (movement is FtdAerialMovement fam)
            {
                wander_distance = fam.WanderDistance.Us;
                roll_goal = Mathf.Abs(yaw_dir) > fam.AngleBeforeTurnAndRollUse.Upper
                    ? -Mathf.Sign(yaw_dir) * fam.RollToExtremeAngle.Us : 0f;
            }
            else
            {
                last_hover_save = false;
                last_yaw_save = false;
                return;
            }
            // TODO: Clean up cam mode.
            pitch_dir = Mathf.Clamp(pitch_dir, -pitch_for_alt, pitch_for_alt);

            float gameTime = GameTimer.Instance.TimeCache;
            float current_alt = subject.CentreOfMass.y;
            float roll_rads = sAngles.z * Mathf.Deg2Rad;

            // TODO: Are these PIDs even okay in multiplayer?
            subject.ControlsRestricted.PlayerControllingNow();
            VariableControllerMaster yawControl = master.Common.YawControl;
            VariableControllerMaster rollControl = master.Common.RollControl;
            VariableControllerMaster pitchControl = master.Common.PitchControl;

            // Player controls hover iff use_hover is true, wasd_dir.y != 0f and current altitude is within AI adjustments.
            if (!last_hover_save || wasd_dir.y != 0 || cam_mode)
                last_hover_alt = lowest_hover_alt = Mathf.Clamp(current_alt, min_alt, max_alt);
            else
                last_hover_alt = Mathf.Min(Mathf.Clamp(last_hover_alt, min_alt, max_alt),
                    Mathf.Max(lowest_hover_alt, min_alt));

            if (wasd_dir.y < 0f && current_alt > min_alt
                || wasd_dir.y > 0f && current_alt < max_alt)
            {
                // TODO: Which line is dupe?
                last_hover_alt = lowest_hover_alt = Mathf.Clamp(current_alt, min_alt, max_alt);
                subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, wasd_dir.y);
                subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight, wasd_dir.y * Mathf.Sin(roll_rads));
            }
            else if (last_hover_save)
            {
                VariableControllerMaster hoverControl = master.Common.HoverControl;
                float alt_goal = hoverControl.NewMeasurement(last_hover_alt, current_alt, gameTime);
                float hover = alt_goal * Mathf.Cos(roll_rads);
                subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, hover);
                subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight, alt_goal * Mathf.Sin(roll_rads));
                if (hover_for_pitch)
                    pitch_dir = -Mathf.Clamp(hover * pitch_for_alt,
                        -pitch_for_alt, pitch_for_alt);
                else if (sTransform.forward.x != 0f || sTransform.forward.z != 0f)
                {
                    Vector3 pitch_focus = Vector3.Normalize(new Vector3(sTransform.forward.x, 0f, sTransform.forward.z)) * wander_distance;
                    pitch_focus.y = last_hover_alt - current_alt;
                    pitch_dir = Mathf.Clamp(VehicleUtils.NormalizeAngle(Quaternion.LookRotation(Vector3.Normalize(pitch_focus)).eulerAngles.x),
                        -pitch_for_alt, pitch_for_alt);
                }
            }

            float pitch_goal = pitchControl.NewMeasurement(pitch_dir, sAngles.x, gameTime);
            float yaw_goal = yawControl.NewMeasurement(yaw_dir + sAngles.y, sAngles.y, gameTime);

            subject.ControlsRestricted.MakeRequest(ControlType.PitchDown, Mathf.Cos(roll_rads) * pitch_goal + Mathf.Sin(roll_rads) * yaw_goal);
            float yaw = Mathf.Cos(roll_rads) * yaw_goal - Mathf.Sin(roll_rads) * pitch_goal;
            subject.ControlsRestricted.MakeRequest(ControlType.YawRight, yaw);
            result += yaw;
            subject.ControlsRestricted.MakeRequest(ControlType.RollLeft, rollControl.NewMeasurement(roll_goal, sAngles.z, gameTime));

            // Forward WS controls
            if (key_map.Bool(KeyInputsFtd.MoveForward, KeyInputEventType.Held)
                && key_map.Bool(KeyInputsFtd.MoveBackward, KeyInputEventType.Held))
            {
                subject.ControlsRestricted.StopDrive(Drive.Main);
                brake_timer.Now(ConstructableController.brakeTime);
            }
            else if (brake_timer.HappenedOrNeverSet)
                subject.ControlsRestricted.MakeRequest(ControlType.PrimaryIncrease, wasd_dir.z);

            last_hover_save = true;
            last_yaw_save = !cam_mode;
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
                    return pitch_alt >= VehicleControllerAircraft.PITCH_REQUIRED
                        && turn_roll >= VehicleControllerAircraft.ROLL_REQUIRED
                        ? VehicleMatch.NO : VehicleMatch.DEFAULT;
                default:
                    return VehicleMatch.DEFAULT;
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
            bool cam_mode = key_map.Bool(KeyInputsFtd.SpeedUpCamera, KeyInputEventType.Held);
            switch (key)
            {
                // Yaw
                case KeyInputsForVehicles.AirYawLeft:
                case KeyInputsForVehicles.WaterYawLeft:
                    return !cam_mode && key_map.Bool(KeyInputsFtd.MoveLeft, KeyInputEventType.Held);
                case KeyInputsForVehicles.AirYawRight:
                case KeyInputsForVehicles.WaterYawRight:
                    return !cam_mode && key_map.Bool(KeyInputsFtd.MoveRight, KeyInputEventType.Held);
                // Pitch
                case KeyInputsForVehicles.AirPitchUp:
                case KeyInputsForVehicles.WaterPitchUp:
                    return !cam_mode && key_map.Bool(KeyInputsFtd.MoveUp, KeyInputEventType.Held);
                case KeyInputsForVehicles.AirPitchDown:
                case KeyInputsForVehicles.WaterPitchDown:
                    return !cam_mode && key_map.Bool(KeyInputsFtd.MoveDown, KeyInputEventType.Held);
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
