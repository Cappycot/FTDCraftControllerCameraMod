using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Control.Pids;
using BrilliantSkies.Ai.Modules.Behaviour;
using BrilliantSkies.Ai.Modules.Behaviour.Examples;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvre.Examples;
using BrilliantSkies.Common.Controls.ConstructModules;
using BrilliantSkies.Core.Timing;
using BrilliantSkies.Ftd.Terrain;
using BrilliantSkies.PlayerProfiles;
using System;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    /// <summary>
    /// Expect a satellite/UFO-type vehicle.
    /// </summary>
    public class VehicleControllerSixAxis : IVehicleController
    {
        private float last_yaw_dir = 0f;
        private bool last_yaw_save = false;
        private float last_hover_alt = 0f;
        private float lowest_hover_alt = 0f;
        private bool last_hover_save = false;

        public void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster aiMaster, IManoeuvre movement, ref float result)
        {
            bool auto_turn = false;
            aiMaster.Pack.GetSelectedBehaviour(out IBehaviour behaviour);
            switch (behaviour)
            {
                case BehaviourCharge _:
                case BehaviourPointAndMaintainDistance _:
                case BehaviourPointAndMaintainDistanceLegacy _:
                    auto_turn = true;
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
            VariableControllerMaster yawControl = aiMaster.Common.YawControl;
            VariableControllerMaster rollControl = aiMaster.Common.RollControl;
            VariableControllerMaster pitchControl = aiMaster.Common.PitchControl;
            VariableControllerMaster hoverControl = aiMaster.Common.HoverControl;
            VariableControllerMaster strafeControl = aiMaster.Common.StrafeControl;
            VariableControllerMaster forwardControl = aiMaster.Common.ForwardBackwardControl;

            float forward;
            float strafe;
            float yaw_dir;
            if (wasd_dir.x != 0f || wasd_dir.z != 0f)
            {
                float camera_y = cameraMode.Transform.eulerAngles.y;
                yaw_dir = auto_turn ? VehicleUtils.NormalizeAngle(camera_y, sAngles.y - 180f, sAngles.y + 180f)
                    : VehicleUtils.NormalizeAngle(Quaternion.LookRotation(wasd_dir, Vector3.up).eulerAngles.y,
                    sAngles.y - 180f, sAngles.y + 180f);
                Vector3 new_dir = Quaternion.Euler(0f, camera_y - sAngles.y, 0f) * wasd_dir;
                forward = new_dir.z;
                strafe = new_dir.x;
                last_yaw_dir = sAngles.y;
            }
            else
            {
                Vector3 normalVelocity = Quaternion.Inverse(sTransform.rotation) * subject.Velocity;
                forward = forwardControl.NewMeasurement(0f, Vector3.Project(normalVelocity, Vector3.forward).z, gameTime);
                strafe = strafeControl.NewMeasurement(0f, Vector3.Project(normalVelocity, Vector3.right).x, gameTime);
                yaw_dir = last_yaw_dir = last_yaw_save ? VehicleUtils.NormalizeAngle(last_yaw_dir, sAngles.y - 180f, sAngles.y + 180f) : sAngles.y;
            }

            float yaw = yawControl.NewMeasurement(yaw_dir, sAngles.y, gameTime);
            subject.ControlsRestricted.MakeRequest(ControlType.YawRight, yaw);
            result += yaw;

            subject.ControlsRestricted.MakeRequest(ControlType.ThrustForward, forward);
            subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight, strafe);

            // Zero pitch and roll.
            subject.ControlsRestricted.MakeRequest(ControlType.PitchDown,
                pitchControl.NewMeasurement(0f, sAngles.x, gameTime));
            subject.ControlsRestricted.MakeRequest(ControlType.RollLeft,
                rollControl.NewMeasurement(0f, sAngles.z, gameTime));

            // Player controls hover iff use_hover is true, wasd_dir.y != 0f and current altitude is within AI adjustments.
            float min_alt = Mathf.Max(StaticTerrainAltitude.AltitudeForGameWorldPositionInMainFrame(sPosition)
                + aiMaster.Adjustments.MinimumAltitudeAboveLand,
                aiMaster.Adjustments.MinimumAltitudeAboveWater);
            float max_alt = Mathf.Max(min_alt, aiMaster.Adjustments.MaximumAltitude);

            last_hover_alt = Mathf.Min(Mathf.Clamp(last_hover_alt, min_alt, max_alt),
                Mathf.Max(lowest_hover_alt, min_alt));
            if (wasd_dir.y < 0f && sPosition.y > min_alt
                || wasd_dir.y > 0f && sPosition.y < max_alt)
            {
                last_hover_alt = lowest_hover_alt = Mathf.Clamp(sPosition.y, min_alt, max_alt);
                subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, wasd_dir.y);
            }
            else if (last_hover_save)
                subject.ControlsRestricted.MakeRequest(ControlType.HoverUp,
                    hoverControl.NewMeasurement(last_hover_alt, sPosition.y, gameTime));
            else
                last_hover_alt = lowest_hover_alt = Mathf.Clamp(sPosition.y, min_alt, max_alt);
            last_hover_save = true;
            last_yaw_save = true;
        }

        public void Enter()
        {
            Reenter();
        }

        public VehicleMatch GetVehicleMatch(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement)
        {
            switch (movement)
            {
                case ManoeuvreSixAxis _:
                    return VehicleMatch.DEFAULT;
                default:
                    return VehicleMatch.NO;
            }
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

        public void Reenter()
        {
            last_hover_save = false;
            last_yaw_save = false;
        }
    }
}
