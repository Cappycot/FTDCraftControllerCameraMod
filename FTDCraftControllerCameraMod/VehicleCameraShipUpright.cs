using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvre.Examples.Ftd;
using BrilliantSkies.Ai.Modules.Manoeuvres;
using BrilliantSkies.PlayerProfiles;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    public class VehicleCameraShipUpright : IVehicleCamera
    {
        public Vector3 GetCameraPosition(CraftCameraMode cameraMode)
        {
            MainConstruct subject = cameraMode.Subject;
            Transform cTransform = cameraMode.Transform;
            Transform sTransform = subject.myTransform;
            HybridZoom zoom = cameraMode.Zoom;
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
            float ud = spaceToMassHeight * 2f + height * (currentZoom - 1f) * 2f;

            Vector3 nforward = Vector3.Normalize(new Vector3(sTransform.forward.x, 0f, sTransform.forward.z));
            Vector3 nright = Vector3.Cross(Vector3.up, nforward);
            // Transform.position = pos + (fb * nforward) + (lr * nright) + (ud * Vector3.up);
            return pos + (fb * nforward) + (lr * nright) + (ud * Vector3.up);
        }

        public VehicleMatch GetVehicleControllerMatch(MainConstruct subject, AIMainframe mainframe, IManoeuvre movement)
        {
            if (movement != null)
            {
                switch (movement)
                {
                    case FortressManoeuvre _:
                    case FtdNavalAndLandManoeuvre _:
                        return VehicleMatch.DEFAULT;
                    case ManoeuvreHover mh:
                        return mh.PitchForForward > 0f || mh.RollForStrafe > 0f
                            ? VehicleMatch.NO : VehicleMatch.DEFAULT;
                    default:
                        return VehicleMatch.NO;
                }
            }
            return subject.GetForce().TravelRestrictions == ForceTravelRestrictions.Air
                ? VehicleMatch.NO : VehicleMatch.DEFAULT;
        }
    }
}
