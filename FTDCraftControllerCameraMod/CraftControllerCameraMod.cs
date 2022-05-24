using BrilliantSkies.Core.Logger;
using BrilliantSkies.Modding;
using HarmonyLib;
using System;
using System.Reflection;

namespace FTDCraftControllerCameraMod
{
    public class Main : GamePlugin
    {
        public string name { get; } = "Craft Controller Camera";
        public Version version { get; } = new Version(1, 0, 0);

        public static CraftCameraMode craftCameraMode = null;

        public void OnLoad()
        {
            Harmony harmony = new Harmony("cappycot.craftcontrollercamera");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            AdvLogger.LogInfo("Craft Controller Camera is loaded.");
        }

        public void OnSave() { }
    }
}
