using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Core.Logger;
using BrilliantSkies.Effects.Cameras;
using BrilliantSkies.Ftd.Avatar;
using BrilliantSkies.Ftd.Avatar.Control;
using BrilliantSkies.Ftd.Avatar.Movement;
using BrilliantSkies.Ftd.Cameras;
using System.Collections.Generic;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    public class CraftCameraMode : ICameraMode
    {
        public bool Valid
        {
            get
            {
                return !cancelled
                    && ccc != null // TODO: Detach cCameraControl dependency maybe.
                    && ccc.CameraState == enumCameraState.firstPerson
                    && movement != null
                    && Subject != null
                    && movement.TheChair()?.MainConstruct == Subject
                    && !Subject.Destroyed;
            }
        }
        public bool SmoothEntrance { get; } = true;
        public bool SmoothExit { get; } = true;
        public Transform Transform { get; set; }

        private bool cancelled = false;
        private cCameraControl ccc;
        private I_cMovement_HUD hud;
        private I_world_cMovement movement;
        public IVehicleCamera vehicleCamera;
        public IVehicleController vehicleController;
        public MouseLook MouseLook { get; private set; }
        public MainConstruct Subject { get; private set; }

        public CraftCameraMode(cCameraControl cCameraControl, I_cMovement_HUD iHUD, MainConstruct mainConstruct)
        {
            ccc = cCameraControl;
            hud = iHUD;
            movement = ClientInterface.GetInterface().Get_I_world_cMovement();
            Subject = mainConstruct;
        }

        public void UpdatePosition()
        {
            if (vehicleCamera != null)
                Transform.position = vehicleCamera.GetCameraPosition(this);
        }

        public void GuessConstructCameraAndControl()
        {
            // Try guessing based on the controller block being used.
            // DIRTY HACK - The type cast *should* succeed, but what if it somehow doesn't?
            ConstructableController controller = (ClientInterface.GetInterface().Get_I_world_cControl() as cControl)?.GetControlModule().ActiveController;
            AiMaster aiMaster = VehicleUtils.GetMovementAiFromMainConstruct(Subject, out IManoeuvre movement);
            AdvLogger.LogInfo(string.Format("Our movement is {0}.", movement?.GetType().Name ?? "none"), LogOptions.Hud);

            // Get best possible camera mode.
            vehicleCamera = (IVehicleCamera)VehicleMatchUtils.GetVehicleMatchable(
                Main.vehicleCameras, this, controller, aiMaster, movement)
                ?? new VehicleCameraDefault();
            AdvLogger.LogInfo(string.Format("Selected camera {0}.", vehicleCamera.GetType().Name), LogOptions.Hud);

            // Get best possible controller mode.
            vehicleController = (IVehicleController)VehicleMatchUtils.GetVehicleMatchable(
                Main.vehicleControllers,this, controller, aiMaster, movement);
            AdvLogger.LogInfo(string.Format("Selected controller {0}.", vehicleController?.GetType().Name ?? "none"), LogOptions.Hud);
        }

        public void Enter(ICameraMode previousMode)
        {
            if (previousMode is cCameraControl)
            {
                // Create Transform.
                GameObject gameObject = new GameObject("Craft Camera");
                MouseLook = gameObject.AddComponent<MouseLook>();
                MouseLook.enabled = false;
                Transform = gameObject.transform;
                // Start transform where the camera was.
                // CameraManager.GetSingleton().MatchTransformToCurrentMode(Transform);
                // Start transform at vehicle's forward rotation.
                float yAngle = Subject.myTransform.eulerAngles.y;
                // We cannot allow (0, 0, 0) due to weird rendering issues.
                Transform.rotation = Quaternion.Euler(yAngle == 0f ? 0.1f : 0f, Subject.myTransform.eulerAngles.y, 0f);
                // Transform.SetRotationWithoutRoll();
                MouseLook.Match();
                Reenter(true);
            }
        }

        public void Exit()
        {
            hud.SetCameraState(ccc.CameraDisplayState);
            // Destroy Transform.
            GameObject.Destroy(Transform.gameObject);
        }

        /// <summary>
        /// Since we only update this camera's position while active,
        /// we need to call UpdatePosition() again to accommodate transitions.
        /// </summary>
        public void Reenter(bool firstEntry)
        {
            // CameraType = CraftCameraType.GuessConstructCameraType(Subject);
            CameraManager.GetSingleton().CancelExternalCameraFocus();
            hud.SetCameraState(enumCameraState.unparented);
            MouseLook.enabled = true;
            GuessConstructCameraAndControl();
            if (firstEntry)
            {
                vehicleCamera.Enter();
                vehicleController?.Enter(); // TODO: Should we make this non-null?
            }
            else
            {
                vehicleCamera.Reenter();
                vehicleController?.Reenter();
            }
            UpdatePosition();
        }

        public void Reenter()
        {
            Reenter(false);
        }

        public void Supersede(ICameraMode nextMode)
        {
            MouseLook.enabled = false;
        }

        public void Cancel()
        {
            cancelled = true;
        }
    }
}
