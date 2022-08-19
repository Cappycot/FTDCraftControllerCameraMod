using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvre.Examples.Ftd;
using BrilliantSkies.Ai.Modules.Manoeuvres;
using BrilliantSkies.PlayerProfiles;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    /// <summary>
    /// General camera positioning for all vehicles that remain upright under normal conditions.
    /// </summary>
    public class VehicleCameraShipUpright : IVehicleCamera
    {
        private HybridZoom zoom = HybridZoom.Exponential(1.5f, 1f, 10f, 0.5f, 0.1f, 5f);

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
            Vector3 angles = cTransform.eulerAngles - sTransform.eulerAngles;

            float length = subject.AllBasics.sz / 2f;
            float width = subject.AllBasics.sx / 2f;
            float height = subject.AllBasics.sy / 2f;
            float currentZoom = zoom.Update(Time.deltaTime);

            float radius = Mathf.Max(length, width);
            float shapeToCircle = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((currentZoom - 1f) / 2f));
            width = Mathf.Lerp(width, radius, shapeToCircle);
            length = Mathf.Lerp(length, radius, shapeToCircle);

            float yrad = Mathf.Deg2Rad * angles.y;
            float lr = width * currentZoom * -Mathf.Sin(yrad);
            float fb = length * currentZoom * -Mathf.Cos(yrad);

            Vector3 pos = sTransform.position
                + sTransform.rotation * (subject.AllBasics.GetCentreOfUsedSpace() - height * Vector3.up);
            float spaceToMassHeight = (Quaternion.Inverse(sTransform.rotation) * (subject.CentreOfMass - pos)).y;
            spaceToMassHeight = Mathf.Min(spaceToMassHeight, height * 2f - spaceToMassHeight);
            // float ud = spaceToMassHeight * 2f + height * (currentZoom - 1f) * 2f;
            // float ud = spaceToMassHeight * 2f + spaceToMassHeight * 2f * (currentZoom - 1f) * 2f;
            float ud = spaceToMassHeight * 2f * (currentZoom * 2f - 1f)
                // Keep camera above centerline.
                + Mathf.Pow(Mathf.Sin(sTransform.eulerAngles.x * Mathf.Deg2Rad), 2f) * length;

            Vector3 nforward = Vector3.Normalize(new Vector3(sTransform.forward.x, 0f, sTransform.forward.z));
            Vector3 nright = Vector3.Cross(Vector3.up, nforward);
            // cTransform.position = pos + (fb * nforward) + (lr * nright) + (ud * Vector3.up);
            return pos + (fb * nforward) + (lr * nright) + (ud * Vector3.up);
        }

        public VehicleMatch GetVehicleMatch(CraftCameraMode cameraMode, ConstructableController controller, AiMaster aiMaster, IManoeuvre movement)
        {
            MainConstruct subject = cameraMode.Subject;
            switch (movement)
            {
                case FortressManoeuvre _:
                case FtdNavalAndLandManoeuvre _:
                    return VehicleMatch.DEFAULT;
                case ManoeuvreAirplane _:
                case FtdAerialMovement _:
                    // Check if the craft is a submarine.
                    // SURELY all submarines stay upright :Clueless:
                    // This is literally the only reason why we might see aircraft AIs on upright watercraft.
                    float min_alt = aiMaster.Adjustments.MinimumAltitudeAboveWater.Us;
                    float max_alt = aiMaster.Adjustments.MaximumAltitude.Us;
                    return min_alt < 0f && Mathf.Abs(min_alt) > max_alt
                        ? VehicleMatch.DEFAULT : VehicleMatch.NO;
                case ManoeuvreHover mh:
                    float pitch = mh.PitchForForward.Us;
                    VehicleUtils.GetMaxPitchFromAiMaster(aiMaster, ref pitch);
                    return pitch > 0f ? VehicleMatch.NO : VehicleMatch.DEFAULT;
                // case ManoeuvreSixAxis _:
                // The following cases should be determined by travel restrictions...
                // case ManoeuvreDefault _:
                default:
                    break;
            }
            return subject.GetForce().TravelRestrictions == ForceTravelRestrictions.Air
                ? VehicleMatch.NO : VehicleMatch.DEFAULT;
        }
    }
}
