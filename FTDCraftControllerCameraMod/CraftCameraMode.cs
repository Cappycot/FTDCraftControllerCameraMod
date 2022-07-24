using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using BrilliantSkies.Effects.Cameras;
using BrilliantSkies.Ftd.Avatar;
using BrilliantSkies.Ftd.Avatar.Control;
using BrilliantSkies.Ftd.Avatar.Movement;
using BrilliantSkies.Ftd.Cameras;
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

        /*// <summary>
        /// General camera positioning for all vehicles
        /// that pitch up and down for altitude and
        /// may roll to turn or strafe.
        /// </summary>
        public void UpdatePositionAir()
        {
            Transform sTransform = Subject.myTransform;
            float length = Subject.AllBasics.sz / 2f;
            float width = Subject.AllBasics.sx / 2f;
            float height = Subject.AllBasics.sy / 2f;

            Vector3 center = sTransform.position + sTransform.rotation * Subject.AllBasics.GetCentreOfUsedSpace();
            Vector3 centerMassToSpace = Quaternion.Inverse(sTransform.rotation) * (center - Subject.CentreOfMass);
            // float centerMassToSpaceHeight = centerMassToSpace.y;
            float spaceToMassHeight = height - Mathf.Abs(centerMassToSpace.y);
            centerMassToSpace.y = 0f;

            float currentZoom = Zoom.Update(Time.deltaTime);
            float radius = (Mathf.Max(length, width) + centerMassToSpace.magnitude)
                * currentZoom;

            Vector3 pos = Subject.CentreOfMass
                + (spaceToMassHeight + height * currentZoom) * Transform.up; // + (centerMassToSpaceHeight + height * currentZoom) * Transform.up;
            Transform.position = pos - Transform.forward * radius;

            /*pos = sTransform.position
                + sTransform.rotation * (Subject.AllBasics.GetCentreOfUsedSpace() - height * Vector3.up);
            float spaceToMassHeight = (Quaternion.Inverse(sTransform.rotation) * (Subject.CentreOfMass - pos)).y;
            spaceToMassHeight = Mathf.Min(spaceToMassHeight, height * 2f - spaceToMassHeight);
            // therefore height = centerMassToSpaceHeight + min spaceToMassHeight
            // need CoM + spaceToMassHeight + height * Transform.up*/
        /*}

        /// <summary>
        /// General camera positioning for all vehicles
        /// that remain upright under normal conditions.
        /// </summary>
        public void UpdatePositionShip()
        {
            Transform sTransform = Subject.myTransform;
            Vector3 angles = Transform.eulerAngles - sTransform.eulerAngles;

            float length = Subject.AllBasics.sz / 2f;
            float width = Subject.AllBasics.sx / 2f;
            float height = Subject.AllBasics.sy / 2f;
            float currentZoom = Zoom.Update(Time.deltaTime);

            float radius = Mathf.Max(length, width);
            float shapeToCircle = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((currentZoom - 1f) / 2f));
            width = Mathf.Lerp(width, radius, shapeToCircle);
            length = Mathf.Lerp(length, radius, shapeToCircle);

            float yrad = Mathf.Deg2Rad * angles.y;
            float lr = width * currentZoom * -Mathf.Sin(yrad);
            float fb = length * currentZoom * -Mathf.Cos(yrad);

            Vector3 pos = sTransform.position
                + sTransform.rotation * (Subject.AllBasics.GetCentreOfUsedSpace() - height * Vector3.up);
            float spaceToMassHeight = (Quaternion.Inverse(sTransform.rotation) * (Subject.CentreOfMass - pos)).y;
            spaceToMassHeight = Mathf.Min(spaceToMassHeight, height * 2f - spaceToMassHeight);
            float ud = spaceToMassHeight * 2f + height * (currentZoom - 1f) * 2f;

            Vector3 nforward = Vector3.Normalize(new Vector3(sTransform.forward.x, 0f, sTransform.forward.z));
            Vector3 nright = Vector3.Cross(Vector3.up, nforward);
            Transform.position = pos + (fb * nforward) + (lr * nright) + (ud * Vector3.up);
        }*/

        public void UpdatePosition()
        {
            /*switch (CameraType)
            {
                case EnumCraftCameraType.AIR_DEFAULT:
                    UpdatePositionAir();
                    break;
                default:
                    UpdatePositionShip();
                    break;
            }*/
            if (vehicleCamera != null)
                Transform.position = vehicleCamera.GetCameraPosition(this);
        }

        public void GuessConstructCameraAndControl()
        {
            // Try guessing based on the controller block being used.
            // DIRTY HACK - The type cast *should* succeed, but what if it somehow doesn't?
            ConstructableController controller = (ClientInterface.GetInterface().Get_I_world_cControl() as cControl)?.GetControlModule().ActiveController;
            // Try guessing based on AI movement type.
            BlockStore<AIMainframe> ais = Subject.iBlockTypeStorage.MainframeStore;
            AiMaster theAI = null;
            IManoeuvre movement = null;
            for (int i = 0; i < ais.Count; i++)
            {
                AiMaster ai = ais.Blocks[i].Node.Master;
                if ((movement == null || theAI == null || ai.Priority > theAI.Priority) && ai.Pack.GetSelectedManoeuvre(out IManoeuvre tempMovement))
                {
                    theAI = ai;
                    movement = tempMovement;
                }
            }
            // Get best possible camera mode.
            IVehicleCamera possibleCamera = vehicleCamera ?? new VehicleCameraDefault();
            VehicleMatch vehicleMatch = VehicleMatch.DEFAULT;
            foreach (IVehicleCamera vc in Main.vehicleCameras)
            {
                VehicleMatch vm = vc.GetVehicleMatch(this, controller, theAI, movement);
                switch (vm)
                {
                    case VehicleMatch.NO: // Not a match.
                        break;
                    case VehicleMatch.MAYBE: // Possible match found. The first MAYBE gets the pick.
                        possibleCamera = vehicleMatch != VehicleMatch.MAYBE ? vc : possibleCamera;
                        vehicleMatch = VehicleMatch.MAYBE;
                        break;
                    case VehicleMatch.YES: // Definite match found. The first YES gets the pick.
                        vehicleMatch = VehicleMatch.YES;
                        possibleCamera = vc;
                        break;
                    default: // VehicleMatch.DEFAULT
                        possibleCamera = vehicleMatch == VehicleMatch.DEFAULT ? vc : possibleCamera;
                        break;
                }
                if (vehicleMatch == VehicleMatch.YES)
                    break;
            }
            vehicleCamera = possibleCamera;

            // Get best possible controller mode.
            // TODO: Make a generic matchable interface.
            vehicleMatch = VehicleMatch.DEFAULT;
            IVehicleController possibleController = vehicleController;
            foreach (IVehicleController vc in Main.vehicleControllers)
            {
                VehicleMatch vm = vc.GetVehicleMatch(this, controller, theAI, movement);
                switch (vm)
                {
                    case VehicleMatch.NO: // Not a match.
                        break;
                    case VehicleMatch.MAYBE: // Possible match found. The first MAYBE gets the pick.
                        possibleController = vehicleMatch != VehicleMatch.MAYBE ? vc : possibleController;
                        vehicleMatch = VehicleMatch.MAYBE;
                        break;
                    case VehicleMatch.YES: // Definite match found. The first YES gets the pick.
                        vehicleMatch = VehicleMatch.YES;
                        possibleController = vc;
                        break;
                    default: // VehicleMatch.DEFAULT
                        possibleController = vehicleMatch == VehicleMatch.DEFAULT ? vc : possibleController;
                        break;
                }
                if (vehicleMatch == VehicleMatch.YES)
                    break;
            }
            vehicleController = possibleController;
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
