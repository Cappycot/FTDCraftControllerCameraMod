using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.PlayerProfiles;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    /// <summary>
    /// If all other vehicle camera modes somehow manage to fail,
    /// we'll pull a Stormworks and just center the camera on the vehicle's CoM.
    /// </summary>
    public class VehicleCameraDefault : IVehicleCamera
    {
        private HybridZoom zoom;

        public void Enter()
        {
            zoom = HybridZoom.Exponential(1.5f, 1f, 10f, 0.5f, 0.1f, 5f);
        }

        public Vector3 GetCameraPosition(CraftCameraMode cameraMode)
        {
            MainConstruct subject = cameraMode.Subject;
            Transform cTransform = cameraMode.Transform;
            float dist = Mathf.Max(subject.AllBasics.sx / 2f, subject.AllBasics.sz / 2f)
                * zoom.Update(Time.deltaTime);
            return subject.CentreOfMass - cTransform.forward * dist;
        }

        public VehicleMatch GetVehicleMatch(CraftCameraMode cameraMode, ConstructableController controller, AiMaster master, IManoeuvre movement)
        {
            return VehicleMatch.NO;
        }

        public void Reenter() { }
    }
}
