using BrilliantSkies.Core.Logger;
using BrilliantSkies.Modding;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FTDCraftControllerCameraMod
{
    public class Main : GamePlugin
    {
        public string name { get; } = "Craft Controller Camera";
        public Version version { get; } = new Version(1, 0, 0);

        public static CraftCameraMode craftCameraMode = null;
        // public static MainConstruct airControlSubject = null; // TODO: Find a key to toggle this.

        public static HashSet<IVehicleCamera> vehicleCameras = new HashSet<IVehicleCamera>();
        public static HashSet<IVehicleController> vehicleControllers = new HashSet<IVehicleController>();

        public void OnLoad()
        {
            // TODO: Register all default vehicle cameras and controllers.
            Harmony harmony = new Harmony("cappycot.craftcontrollercamera");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            AdvLogger.LogInfo("Craft Controller Camera is loaded.");
        }

        public void OnSave() { }
    }
}
