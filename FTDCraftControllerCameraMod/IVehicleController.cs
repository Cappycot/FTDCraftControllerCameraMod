using BrilliantSkies.Ai.Modules.Manoeuvre;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    public interface IVehicleController
    {
        VehicleMatch GetVehicleControllerMatch(MainConstruct subject, AIMainframe mainframe, IManoeuvre movement);
        void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, ref float result);
    }
}