using BrilliantSkies.Effects.Cameras;
using BrilliantSkies.Ftd.Avatar;
using BrilliantSkies.Ftd.Avatar.Movement;
using BrilliantSkies.Ftd.Cameras;
using BrilliantSkies.PlayerProfiles;
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
        public MainConstruct Subject { get; private set; }
        public EnumCraftCameraType CameraType { get; private set; } = 0;
        private MouseLook mouseLook;
        private HybridZoom zoom;

        public CraftCameraMode(cCameraControl cCameraControl, I_cMovement_HUD iHUD, MainConstruct mainConstruct)
        {
            ccc = cCameraControl;
            hud = iHUD;
            movement = ClientInterface.GetInterface().Get_I_world_cMovement();
            Subject = mainConstruct;
            zoom = HybridZoom.Exponential(1.5f, 1f, 10f, 0.5f, 0.1f, 5f);
        }

        /// <summary>
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

            float currentZoom = zoom.Update(Time.deltaTime);
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
        }

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
            float currentZoom = zoom.Update(Time.deltaTime);

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
        }

        public void UpdatePosition()
        {
            switch (CameraType)
            {
                case EnumCraftCameraType.AIR_DEFAULT:
                    UpdatePositionAir();
                    break;
                default:
                    UpdatePositionShip();
                    break;
            }
        }

        public void Enter(ICameraMode previousMode)
        {
            if (previousMode is cCameraControl)
            {
                // Create Transform.
                GameObject gameObject = new GameObject("Craft Camera");
                mouseLook = gameObject.AddComponent<MouseLook>();
                mouseLook.enabled = false;
                Transform = gameObject.transform;
                // Start transform where the camera was.
                // CameraManager.GetSingleton().MatchTransformToCurrentMode(Transform);
                // Start transform at vehicle's forward rotation.
                Transform.rotation = Quaternion.Euler(0f, Subject.myTransform.eulerAngles.y, 0f);
                // Transform.SetRotationWithoutRoll();
                mouseLook.Match();
                Reenter();
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
        public void Reenter()
        {
            CameraType = CraftCameraType.GuessConstructCameraType(Subject);
            CameraManager.GetSingleton().CancelExternalCameraFocus();
            hud.SetCameraState(enumCameraState.unparented);
            mouseLook.enabled = true;
            UpdatePosition();
        }

        public void Supersede(ICameraMode nextMode)
        {
            mouseLook.enabled = false;
        }

        public void Cancel()
        {
            cancelled = true;
        }
    }
}
