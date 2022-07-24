using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    public interface IVehicleCamera : IVehicleMatchable
    {
        Vector3 GetCameraPosition(CraftCameraMode cameraMode);
        void Enter();
        void Reenter();
    }
}