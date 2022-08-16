using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvre.Examples.Ftd;
using BrilliantSkies.Ai.Modules.Manoeuvres;
using BrilliantSkies.PlayerProfiles;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    /// <summary>
    /// General camera positioning for all vehicles
    /// that pitch up and down for altitude and
    /// may roll to turn or strafe.
    /// </summary>
    public class VehicleCameraAirDefault : IVehicleCamera
    {
        private HybridZoom zoom;

        public void Enter()
        {
            zoom = HybridZoom.Exponential(1.5f, 1f, 10f, 0.5f, 0.1f, 5f);
        }

        public void Reenter() { }

        public Vector3 GetCameraPosition(CraftCameraMode cameraMode)
        {
            MainConstruct subject = cameraMode.Subject;
            Transform cTransform = cameraMode.Transform;
            Transform sTransform = subject.myTransform;
            float length = subject.AllBasics.sz / 2f;
            float width = subject.AllBasics.sx / 2f;
            float height = subject.AllBasics.sy / 2f;

            Vector3 center = sTransform.position + sTransform.rotation * subject.AllBasics.GetCentreOfUsedSpace();
            Vector3 centerMassToSpace = Quaternion.Inverse(sTransform.rotation) * (center - subject.CentreOfMass);
            // float centerMassToSpaceHeight = centerMassToSpace.y;
            float spaceToMassHeight = height - Mathf.Abs(centerMassToSpace.y);
            centerMassToSpace.y = 0f;

            float currentZoom = zoom.Update(Time.deltaTime);
            float radius = (Mathf.Max(length, width) + centerMassToSpace.magnitude)
                * currentZoom;

            Vector3 pos = subject.CentreOfMass
                + (spaceToMassHeight + height * currentZoom) * cTransform.up; // + (centerMassToSpaceHeight + height * currentZoom) * Transform.up;
            // cTransform.position = pos - cTransform.forward * radius;
            return pos - cTransform.forward * radius;

            /*pos = sTransform.position
                + sTransform.rotation * (Subject.AllBasics.GetCentreOfUsedSpace() - height * Vector3.up);
            float spaceToMassHeight = (Quaternion.Inverse(sTransform.rotation) * (Subject.CentreOfMass - pos)).y;
            spaceToMassHeight = Mathf.Min(spaceToMassHeight, height * 2f - spaceToMassHeight);
            // therefore height = centerMassToSpaceHeight + min spaceToMassHeight
            // need CoM + spaceToMassHeight + height * Transform.up*/
        }

        public VehicleMatch GetVehicleMatch(CraftCameraMode cameraMode, ConstructableController controller, AiMaster master, IManoeuvre movement)
        {
            MainConstruct subject = cameraMode.Subject;
            if (movement != null)
            {
                switch (movement)
                {
                    case FortressManoeuvre _:
                    case FtdNavalAndLandManoeuvre _:
                        return VehicleMatch.NO;
                    case ManoeuvreAirplane _:
                    case FtdAerialMovement _:
                        return VehicleMatch.DEFAULT;
                    case ManoeuvreHover mh:
                        return mh.PitchForForward > 0f || mh.RollForStrafe > 0f
                            ? VehicleMatch.DEFAULT : VehicleMatch.NO;
                    // The following cases should be determined by travel restrictions...
                    // case ManoeuvreSixAxis _:
                    // case ManoeuvreDefault _:
                    default:
                        break;
                }
            }
            if (controller != null)
            {
                switch (controller.Data.Type.Us)
                {
                    case enumConstructableControllerModes.fortress:
                    case enumConstructableControllerModes.waterSimple:
                        return VehicleMatch.NO;
                    // The following cases seems to be inconclusive based on KoTL designs...
                    // case enumConstructableControllerModes.air:
                    // case enumConstructableControllerModes.spinblock:
                    // case enumConstructableControllerModes.thruster:
                    default:
                        break;
                }
            }
            return subject.GetForce().TravelRestrictions == ForceTravelRestrictions.Air
                ? VehicleMatch.DEFAULT : VehicleMatch.NO;
        }
    }
}
