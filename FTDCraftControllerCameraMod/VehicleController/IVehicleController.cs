using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;

namespace FTDCraftControllerCameraMod
{
    public interface IVehicleController : IVehicleMatchable
    {
        void ControlVehicle(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster aiMaster, IManoeuvre movement, ref float result);
        void Enter();
        void Reenter();
    }
}