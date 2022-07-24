﻿using BrilliantSkies.Core.Logger;
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
            Harmony harmony = new Harmony("cappycot.craftcontrollercamera");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            // TODO: Make and register all default vehicle cameras and controllers.
            vehicleCameras.Add(new VehicleCameraShipUpright());
            vehicleCameras.Add(new VehicleCameraAirDefault());
            vehicleControllers.Add(new VehicleControllerAircraft());
            vehicleControllers.Add(new VehicleControllerHoverFront());
            AdvLogger.LogInfo("Craft Controller Camera is loaded.");
        }

        public void OnSave() { }
    }
}
