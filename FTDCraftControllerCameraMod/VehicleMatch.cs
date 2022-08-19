using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using System.Collections.Generic;

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

    public class VehicleMatchUtils
    {
        public static IVehicleMatchable GetVehicleMatchable(IEnumerable<IVehicleMatchable> collection, CraftCameraMode cameraMode, ConstructableController constructableController, AiMaster master, IManoeuvre movement)
        {
            IVehicleMatchable possibleMatchable = null;
            VehicleMatch vehicleMatch = VehicleMatch.DEFAULT;
            foreach (IVehicleMatchable vc in collection)
            {
                VehicleMatch vm = vc.GetVehicleMatch(cameraMode, constructableController, master, movement);
                switch (vm)
                {
                    case VehicleMatch.NO: // Not a match.
                        break;
                    case VehicleMatch.MAYBE: // Possible match found. The first MAYBE gets the pick.
                        possibleMatchable = vehicleMatch != VehicleMatch.MAYBE ? vc : possibleMatchable;
                        vehicleMatch = VehicleMatch.MAYBE;
                        break;
                    case VehicleMatch.YES: // Definite match found. The first YES gets the pick.
                        return vc;
                    default: // VehicleMatch.DEFAULT
                        possibleMatchable = vehicleMatch == VehicleMatch.DEFAULT ? vc : possibleMatchable;
                        break;
                }
            }
            return possibleMatchable;
        }
    }
}
