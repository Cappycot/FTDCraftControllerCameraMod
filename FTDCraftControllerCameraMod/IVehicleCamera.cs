using BrilliantSkies.Ai.Modules.Manoeuvre;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    public interface IVehicleCamera
    {
        VehicleMatch GetVehicleControllerMatch(MainConstruct subject, AIMainframe mainframe, IManoeuvre movement);
        Vector3 GetCameraPosition(CraftCameraMode cameraMode);
    }
}