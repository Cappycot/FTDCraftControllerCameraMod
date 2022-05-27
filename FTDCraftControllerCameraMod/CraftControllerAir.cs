using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Control.Pids;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvre.Examples.Ftd;
using BrilliantSkies.Common.Controls.ConstructModules;
using BrilliantSkies.Core.Timing;
using BrilliantSkies.Ftd.Terrain;
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

                float rollDeadzone = 1f;
                switch (movement)
                {
                    case ManoeuvreAirplane ma:
                        rollDeadzone = Mathf.Max(rollDeadzone, ma.BankingTurnAbove.Us);
                        break;
                    case FtdAerialMovement fam:
                        rollDeadzone = Mathf.Max(rollDeadzone, fam.BankingTurnAbove.Us);
                        break;
                    default:
                        break;
                }

                subject.ControlsRestricted.PlayerControllingNow();
                VariableControllerMaster yawControl = theAI.Common.YawControl;
                VariableControllerMaster rollControl = theAI.Common.RollControl;
                VariableControllerMaster pitchControl = theAI.Common.PitchControl;
                /*VariableControllerMaster strafeControl = theAI.Common.StrafeControl;
                VariableControllerMaster forwardBackwardControl = theAI.Common.ForwardBackwardControl;
                VariableControllerMaster hoverControl = theAI.Common.HoverControl;*/

                // Get altitude restrictions at focus point.
                Transform cTransform = Main.craftCameraMode.Transform;
                Vector3 focusPoint = cTransform.position + FOCUS_DISTANCE * cTransform.forward;
                float landHeight = StaticTerrainAltitude.AltitudeForGameWorldPositionInMainFrame(focusPoint)
                    + theAI.Adjustments.MinimumAltitudeAboveLand;
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
                goalEula = NormalizeAngles(goalEula);

                // Calculate roll to pitch turn.
                float rollLimit = Mathf.Abs(Mathf.Sin(Mathf.Deg2Rad * goalEula.y) * 90f);
                float rollTest = NormalizeAngle(-rollRotation.eulerAngles.y);
                rollTest = Mathf.Clamp(rollTest, -rollLimit, rollLimit);
                if (Mathf.Abs(goalEula.y) > rollDeadzone)
                    goalEula.z = rollTest;

                // Use AI PID to control.
                float yaw = yawControl.NewMeasurement(goalEula.y, 0f, GameTimer.Instance.TimeCache);
                float pitch = pitchControl.NewMeasurement(goalEula.x, 0f, GameTimer.Instance.TimeCache);
                float roll = rollControl.NewMeasurement(goalEula.z, 0f, GameTimer.Instance.TimeCache);
                // -1 is yaw left, +1 is yaw right
                subject.ControlsRestricted.MakeRequest(pitch < 0f ? ControlType.PitchUp : ControlType.PitchDown, Mathf.Abs(pitch));
                subject.ControlsRestricted.MakeRequest(yaw < 0f ? ControlType.YawLeft : ControlType.YawRight, Mathf.Abs(yaw));
                __result += yaw;
                subject.ControlsRestricted.MakeRequest(roll < 0f ? ControlType.RollRight : ControlType.RollLeft, Mathf.Abs(roll));
            }
        }

        public static Vector3 NormalizeAngles(Vector3 vector3)
        {
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
