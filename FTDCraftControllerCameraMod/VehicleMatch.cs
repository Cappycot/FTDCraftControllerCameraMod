namespace FTDCraftControllerCameraMod
{
    public enum VehicleMatch
    {
        DEFAULT,
        NO, // The camera/controller does not fit this vehicle profile.
        MAYBE, // This camera/controller fits the vehicle profile, but
               // there may be better options.
        YES // Stop the search and use this camera/controller.
    }
}
