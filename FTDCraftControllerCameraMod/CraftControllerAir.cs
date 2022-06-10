using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Control.Pids;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvre.Examples.Ftd;
using BrilliantSkies.Common.Controls.ConstructModules;
using BrilliantSkies.Core.Timing;
using BrilliantSkies.Ftd.Terrain;
using BrilliantSkies.PlayerProfiles;
using HarmonyLib;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    [HarmonyPatch(typeof(ConstructableController))]
    public class ConstructableControllerAirFlight
    {
        public static readonly float PITCH_UP_TO = -315f;
        public static readonly float PITCH_DOWN_TO = 45f;
        public static readonly float FOCUS_DISTANCE = 500f;
        public static readonly Quaternion FORWARD_TO_UP = Quaternion.FromToRotation(Vector3.forward, Vector3.up);

        private static float last_hover_height = 0f;
        private static bool last_hover_save = false;

        [HarmonyPatch("AirStuff")]
        [HarmonyPostfix]
        public static void CameraAirControl(ConstructableController __instance, ref float __result)
        {
            if (CameraManager.GetSingleton().CurrentMode == Main.craftCameraMode
                && Main.craftCameraMode.CameraType == EnumCraftCameraType.AIR_DEFAULT
                // && Main.airControlSubject == Main.craftCameraMode.Subject
                && Main.craftCameraMode.Subject == __instance.MainConstruct)
            {
                IMainConstructBlock subject = __instance.MainConstruct;

                // Check for AI and get PID parameters.
                BlockStore<AIMainframe> ais = subject.iBlockTypeStorage.MainframeStore;
                AiMaster theAI = null;
                IManoeuvre movement = null;
                for (int i = 0; i < ais.Count; i++)
                    if ((theAI = ais.Blocks[i].Node.Master).Pack.GetSelectedManoeuvre(out movement))
                        break;
                if (movement == null)
                    return;

                bool hover_test = false;
                float current_alt = 0f;
                float rollDeadzone = 1f;
                float pitchToThrust = 0f;
                float rollToStrafe = 0f;
                Vector3 wasd_dir = ProfileManager.Instance.GetModule<FtdKeyMap>().GetMovementDirection(false);
                switch (movement)
                {
                    case ManoeuvreAirplane ma:
                        rollDeadzone = Mathf.Max(rollDeadzone, ma.BankingTurnAbove.Us);
                        break;
                    case FtdAerialMovement fam:
                        rollDeadzone = Mathf.Max(rollDeadzone, fam.BankingTurnAbove.Us);
                        break;
                    case ManoeuvreHover mh:
                        hover_test = true;
                        current_alt = subject.myTransform.position.y;
                        pitchToThrust = mh.PitchForForward.Us;
                        rollToStrafe = mh.RollForStrafe.Us;
                        rollDeadzone = Mathf.Max(rollDeadzone, mh.MoveWithinAzi.Us);
                        break;
                    default:
                        break;
                }

                subject.ControlsRestricted.PlayerControllingNow();
                VariableControllerMaster yawControl = theAI.Common.YawControl;
                VariableControllerMaster rollControl = theAI.Common.RollControl;
                VariableControllerMaster pitchControl = theAI.Common.PitchControl;
                /*VariableControllerMaster strafeControl = theAI.Common.StrafeControl;
                VariableControllerMaster forwardBackwardControl = theAI.Common.ForwardBackwardControl;*/
                VariableControllerMaster hoverControl = theAI.Common.HoverControl;

                // Get altitude restrictions at focus point.
                Transform cTransform = Main.craftCameraMode.Transform;
                Vector3 focusPoint = cTransform.position + FOCUS_DISTANCE * cTransform.forward;
                float landHeight = StaticTerrainAltitude.AltitudeForGameWorldPositionInMainFrame(focusPoint)
                    + theAI.Adjustments.MinimumAltitudeAboveLand;
                if (!hover_test)
                    focusPoint.y = Mathf.Clamp(focusPoint.y,
                        Mathf.Max(landHeight, theAI.Adjustments.MinimumAltitudeAboveWater),
                        theAI.Adjustments.MaximumAltitude);

                // Calculate pitch, yaw, roll for vehicle to camera.
                Transform sTransform = subject.myTransform;
                Vector3 dir = Vector3.Normalize(focusPoint - subject.CentreOfMass);
                Quaternion cameraRotation = Quaternion.LookRotation(dir, Vector3.up);
                Quaternion goalRotation = Quaternion.Inverse(sTransform.rotation) * cameraRotation;
                Quaternion rollRotation = Quaternion.Inverse(sTransform.rotation
                    * FORWARD_TO_UP) * cameraRotation;
                Vector3 goalEula = goalRotation.eulerAngles;
                goalEula = NormalizeAngles(goalEula, hover_test);
                goalEula.x += wasd_dir.z * pitchToThrust; // Hover movement pitches for forward/backward movement.

                // Calculate roll to pitch turn.
                float rollLimit = Mathf.Abs(Mathf.Sin(Mathf.Deg2Rad * goalEula.y) * 90f);
                float rollTest = NormalizeAngle(-rollRotation.eulerAngles.y);
                rollTest = Mathf.Clamp(rollTest, -rollLimit, rollLimit);
                bool rolling = Mathf.Abs(goalEula.y) > rollDeadzone;
                if (rolling)
                    goalEula.z = rollTest;
                else if (hover_test)
                    goalEula.z -= wasd_dir.x * rollToStrafe; // Hover movement rolls for left/right movement.

                // Use AI PID to control.
                float yaw = yawControl.NewMeasurement(goalEula.y, 0f, GameTimer.Instance.TimeCache);
                float pitch = pitchControl.NewMeasurement(goalEula.x, 0f, GameTimer.Instance.TimeCache);
                float roll = rollControl.NewMeasurement(goalEula.z, 0f, GameTimer.Instance.TimeCache);
                // -1 is yaw left, +1 is yaw right
                // subject.ControlsRestricted.MakeRequest(pitch < 0f ? ControlType.PitchUp : ControlType.PitchDown, Mathf.Abs(pitch));
                subject.ControlsRestricted.MakeRequest(ControlType.PitchDown, pitch);
                // subject.ControlsRestricted.MakeRequest(yaw < 0f ? ControlType.YawLeft : ControlType.YawRight, Mathf.Abs(yaw));
                subject.ControlsRestricted.MakeRequest(ControlType.YawRight, yaw);
                __result += yaw;
                // subject.ControlsRestricted.MakeRequest(roll < 0f ? ControlType.RollRight : ControlType.RollLeft, Mathf.Abs(roll));
                subject.ControlsRestricted.MakeRequest(ControlType.RollLeft, roll);

                // Test WASD
                float forward = wasd_dir.z;
                subject.ControlsRestricted.MakeRequest(ControlType.StrafeRight, wasd_dir.x);
                if (hover_test && last_hover_save && !rolling && wasd_dir.y == 0f)
                {
                    float hover = hoverControl.NewMeasurement(last_hover_height, current_alt, GameTimer.Instance.TimeCache);
                    float hover_pitch = NormalizeAngle(sTransform.eulerAngles.x);
                    subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, hover
                        * Mathf.Cos(hover_pitch * Mathf.Deg2Rad)
                        * Mathf.Cos(sTransform.eulerAngles.z * Mathf.Deg2Rad));
                    // Use forward/backward thrust to control altitude when rotated.
                    if (forward == 0f && Mathf.Abs(hover_pitch) > pitchToThrust)
                        forward = -hover * Mathf.Sin(hover_pitch * Mathf.Deg2Rad);
                }
                else
                {
                    subject.ControlsRestricted.MakeRequest(ControlType.HoverUp, wasd_dir.y);
                    last_hover_height = current_alt;
                }
                last_hover_save = hover_test;
                subject.ControlsRestricted.MakeRequest(ControlType.ThrustForward, forward);
            }
            else
                last_hover_save = false;
        }

        public static Vector3 NormalizeAngles(Vector3 vector3, bool hover_test)
        {
            if (hover_test)
                vector3.x = NormalizeAngle(vector3.x);
            else
                vector3.x = NormalizeAngle(vector3.x, PITCH_UP_TO, PITCH_DOWN_TO); // TODO: Determine how to pick these numbers.
            vector3.y = NormalizeAngle(vector3.y);
            vector3.z = NormalizeAngle(vector3.z);
            return vector3;
        }

        public static float NormalizeAngle(float angle, float min = -180f, float max = 180f)
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
