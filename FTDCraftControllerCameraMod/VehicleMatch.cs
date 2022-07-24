using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;

namespace FTDCraftControllerCameraMod
{
    public enum VehicleMatch
    {
        DEFAULT, // Note: The last DEFAULT gets the pick.
        NO, // The camera/controller does not fit this vehicle profile.
        MAYBE, // This camera/controller fits the vehicle profile, but
               // there may be better options.
        YES // Stop the search and use this camera/controller.
    }

    public interface IVehicleMatchable
    {
        VehicleMatch GetVehicleMatch(CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement);
    }
}
