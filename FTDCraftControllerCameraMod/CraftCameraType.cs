﻿using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Ai.Modules.Manoeuvre.Examples.Ftd;
using BrilliantSkies.Ai.Modules.Manoeuvres;
using BrilliantSkies.Ftd.Avatar;
using BrilliantSkies.Ftd.Avatar.Control;

namespace FTDCraftControllerCameraMod
{
    public enum EnumCraftCameraType
    {
        AIR_DEFAULT, // Center origin at COM plus half the craft height
        SHIP_DEFAULT // Center origin at twice the height difference
                     // between the COM and bottom of craft
    }

    public class CraftCameraType
    {
        public static EnumCraftCameraType GuessConstructCameraType(MainConstruct mainConstruct)
        {
            // Try guessing based on the controller block being used.
            // DIRTY HACK - The type cast *should* succeed, but what if it somehow doesn't?
            ConstructableController controller = (ClientInterface.GetInterface().Get_I_world_cControl() as cControl)?.GetControlModule().ActiveController;
            if (controller != null)
            {
                switch (controller.Data.Type.Us)
                {
                    case enumConstructableControllerModes.fortress:
                    case enumConstructableControllerModes.waterSimple:
                        return EnumCraftCameraType.SHIP_DEFAULT;
                    // The following cases seems to be inconclusive based on KoTL designs...
                    // case enumConstructableControllerModes.air:
                    // case enumConstructableControllerModes.spinblock:
                    // case enumConstructableControllerModes.thruster:
                    default:
                        break;
                }
            }

            // Try guessing based on AI movement type.
            BlockStore<AIMainframe> ais = mainConstruct.iBlockTypeStorage.MainframeStore;
            AiMaster theAI = null;
            IManoeuvre movement = null;
            for (int i = 0; i < ais.Count; i++)
                if ((theAI = ais.Blocks[i].Node.Master).Pack.GetSelectedManoeuvre(out movement))
                    break;
            if (movement != null)
            {
                switch (movement)
                {
                    case FortressManoeuvre _:
                    case FtdNavalAndLandManoeuvre _:
                        return EnumCraftCameraType.SHIP_DEFAULT;
                    case ManoeuvreAirplane _:
                    case FtdAerialMovement _:
                        // Check if the craft is a fuckin' sub.
                        // SURELY all submarines stay upright :Clueless:
                        // This is literally the only reason why we might see aircraft AIs on watercraft.
                        return theAI.Adjustments.MinimumAltitudeAboveWater.Us < 0
                            ? EnumCraftCameraType.SHIP_DEFAULT : EnumCraftCameraType.AIR_DEFAULT;
                    case ManoeuvreHover mh:
                        // You're probably a ship if your AI is set to restrict pitch and roll... right?
                        return mh.PitchForForward > 0f && mh.RollForStrafe > 0f
                            ? EnumCraftCameraType.AIR_DEFAULT : EnumCraftCameraType.SHIP_DEFAULT;
                    // The following cases should be determined by travel restrictions...
                    // case ManoeuvreSixAxis _:
                    // case ManoeuvreDefault _:
                    default:
                        break;
                }
            }

            // Default to user waypointing restrictions for force.
            return mainConstruct.GetForce().TravelRestrictions == ForceTravelRestrictions.Air
                ? EnumCraftCameraType.AIR_DEFAULT : EnumCraftCameraType.SHIP_DEFAULT;
        }
    }
}