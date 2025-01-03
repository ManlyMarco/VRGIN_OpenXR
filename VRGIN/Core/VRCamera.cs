using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SpatialTracking;
using Valve.VR;
using VRGIN.Helpers;

namespace VRGIN.Core
{
    public class VRCamera : ProtectedBehaviour
    {
        public Camera MainCamera => _Camera;

        private delegate void CameraOperation(Camera camera);

        private static VRCamera _Instance;

        private IList<CameraSlave> Slaves = new List<CameraSlave>();

        private const float MIN_FAR_CLIP_PLANE = 10f;

        private Camera _Camera;


        private Transform _origin;

        private Transform _head;

        public SteamVR_Camera SteamCam { get; private set; }

        public TrackedPoseDriver TrackedPose { get; private set; }

        public Camera Blueprint
        {
            get
            {
                if (!_Blueprint || !_Blueprint.isActiveAndEnabled) return Slaves.Select((CameraSlave s) => s.Camera).FirstOrDefault((Camera c) => !VR.GUI.Owns(c));
                return _Blueprint;
            }
        }

        private Camera _Blueprint { get; set; }

        public bool HasValidBlueprint => Slaves.Count > 0;

        public Transform Origin => SteamCam.origin;

        public Transform Head => SteamCam.head;

        public static VRCamera Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new GameObject("VRGIN_Camera").AddComponent<AudioListener>().gameObject.AddComponent<VRCamera>();
                    DontDestroyOnLoad(_Instance.gameObject);
                }

                return _Instance;
            }
        }

        public event EventHandler<InitializeCameraEventArgs> InitializeCamera = delegate { };

        public event EventHandler<CopiedCameraEventArgs> CopiedCamera = delegate { };

        protected override void OnAwake()
        {
            _Camera = gameObject.AddComponent<Camera>();

            // Why deceive Unity and the game with wrong Main Camera?
            // simply because of this we had quite a few "fixes" in KKS.
            //_Camera.tag = "MainCamera";

            gameObject.AddComponent<SteamVR_Camera>();
            TrackedPose = gameObject.AddComponent<TrackedPoseDriver>();
            TrackedPose.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
            TrackedPose.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            TrackedPose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            SteamCam = GetComponent<SteamVR_Camera>();
            _ = VR.Settings.MirrorScreen;
            SteamVR_Camera.sceneResolutionScale = VR.Settings.RenderScale;
            new GameObject("CenterEyeAnchor").transform.SetParent(SteamCam.head);
            DontDetroyRootObject(SteamCam.origin.gameObject);
        }

        private void DontDetroyRootObject(GameObject go)
        {
            while (go.transform.parent != null) go = go.transform.gameObject;
            DontDestroyOnLoad(go);
        }

        /// <summary>
        /// Copies the values of a in-game camera to the VR camera.
        /// </summary>
        /// <param name="blueprint">The camera to copy.</param>
        public void Copy(Camera blueprint, bool canBeMain = false, bool hasOtherConsumers = false)
        {
            VRLog.Info("Copying camera: {0}", blueprint ? blueprint.name : "NULL");

            if (blueprint && blueprint.GetComponent<CameraSlave>())
            {
                VRLog.Warn("Is already slave -- NOOP");
                return;
            }

            if (canBeMain && UseNewCamera(blueprint))
            {
                ChangeBlueprint(blueprint);
            }

            if(blueprint)
            {
                blueprint.gameObject.AddComponent<CameraSlave>().canBeMainCamera = canBeMain;

                // Highlander principle
                var listener = blueprint.GetComponent<AudioListener>();
                if (listener)
                {
                    Destroy(listener);
                }

                // Prevent Unity from moving this camera around.
                blueprint.stereoTargetEye = StereoTargetEyeMask.None;

                if (!hasOtherConsumers && blueprint.targetTexture == null && VR.Interpreter.IsIrrelevantCamera(blueprint))
                {
                    //StartCoroutine(ExecuteDelayed(delegate { CopyFX(Blueprint); }));
                    //CopyFX(Blueprint);

                    blueprint.gameObject.AddComponent<CameraKiller>();
                    //blueprint.enabled = false;
                    //blueprint.nearClipPlane = Blueprint.farClipPlane = 0;

                    //Blueprint.targetTexture = _MiniTexture;
                    //Blueprint.gameObject.AddComponent<BlacklistThrottler>();
                }
            }

            if(canBeMain)
            {
                // Hook
                InitializeCamera(this, new InitializeCameraEventArgs(GetComponent<Camera>(), Blueprint));
            }

            CopiedCamera(this, new CopiedCameraEventArgs(blueprint));
        }

        private void ChangeBlueprint(Camera blueprint)
        {
            _Blueprint = blueprint;

            var camera = SteamCam.GetComponent<Camera>();
            camera.nearClipPlane = VR.Context.NearClipPlane;
            camera.farClipPlane = Mathf.Max(Blueprint.farClipPlane, MIN_FAR_CLIP_PLANE);
            camera.clearFlags = Blueprint.clearFlags == CameraClearFlags.Skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.renderingPath = Blueprint.renderingPath;
            camera.clearStencilAfterLightingPass = Blueprint.clearStencilAfterLightingPass;
            camera.depthTextureMode = Blueprint.depthTextureMode;
            camera.layerCullDistances = Blueprint.layerCullDistances;
            camera.layerCullSpherical = Blueprint.layerCullSpherical;
            camera.useOcclusionCulling = Blueprint.useOcclusionCulling;
            camera.allowHDR = Blueprint.allowHDR;

            camera.backgroundColor = Blueprint.backgroundColor;

            var skybox = Blueprint.GetComponent<Skybox>();
            if (skybox != null)
            {
                var vrSkybox = camera.gameObject.GetComponent<Skybox>();
                if (vrSkybox == null) vrSkybox = vrSkybox.gameObject.AddComponent<Skybox>();

                vrSkybox.material = skybox.material;
            }
        }

        private bool UseNewCamera(Camera blueprint)
        {
            if(_Blueprint && _Blueprint != _Camera && _Blueprint != blueprint && _Blueprint.name == "Main Camera")
            {
                VRLog.Info("Using {0} over {1} as main camera", _Blueprint.name, blueprint.name);
                return false;
            }

            return true;
        }
        public void UpdateCameraBlueprint()
        {
            var bestSlave = Slaves.Reverse().FirstOrDefault((slave) => slave.canBeMainCamera);
            if (bestSlave)
            {
                ChangeBlueprint(bestSlave.Camera);
            }
        }
        private void UpdateCameraConfig()
        {
            // Null ref here is most likely due to KKS_AmazingNewAccessoryLogic and all the cameras it adds.
            int cullingMask = Slaves.Aggregate(0, (cull, cam) => cull | cam.cullingMask);
            // Remove layers that are captured by other cameras (see VRGUI)
            cullingMask |= VR.Interpreter.DefaultCullingMask;
            cullingMask &= ~(LayerMask.GetMask(VR.Context.UILayer, VR.Context.InvisibleLayer));
            cullingMask &= ~(VR.Context.IgnoreMask);

            VRLog.Info("The camera sees {0} ({1})", string.Join(", ", UnityHelper.GetLayerNames(cullingMask)), string.Join(", ", Slaves.Select(s => s.name).ToArray()));

            GetComponent<Camera>().cullingMask = cullingMask;

            if (!Slaves.Any((slave) => slave.Camera == _Blueprint))
            {
                // Try to find a new main camera.
                UpdateCameraBlueprint();
            }
        }

        public void CopyFX(Camera blueprint)
        {
            CopyFX(blueprint.gameObject, gameObject, false);
            FixEffectOrder();
        }

        public void FixEffectOrder()
        {
            if (!SteamCam) SteamCam = GetComponent<SteamVR_Camera>();
            SteamCam.ForceLast();
            SteamCam = GetComponent<SteamVR_Camera>();
        }

        private void CopyFX(GameObject source, GameObject target, bool disabledSourceFx = false)
        {
            foreach (var cameraEffect in target.GetCameraEffects()) DestroyImmediate(cameraEffect);
            var num = target.GetComponents<Component>().Length;
            VRLog.Info("Copying FX to {0}...", target.name);
            foreach (var cameraEffect2 in source.GetCameraEffects())
            {
                if (VR.Interpreter.IsAllowedEffect(cameraEffect2))
                {
                    VRLog.Info("Copy FX: {0} (enabled={1})", cameraEffect2.GetType().Name, cameraEffect2.enabled);
                    var monoBehaviour = target.CopyComponentFrom(cameraEffect2);
                    if(monoBehaviour) VRLog.Info("Attached!");
                    monoBehaviour.enabled = cameraEffect2.enabled;
                }
                else
                    VRLog.Info("Skipping image effect {0}", cameraEffect2.GetType().Name);

                if (disabledSourceFx) cameraEffect2.enabled = false;
            }

            VRLog.Info("{0} components before the additions, {1} after", num, target.GetComponents<Component>().Length);
        }

        //protected override void OnUpdate()
        //{
        //    if (SteamCam.origin) SteamCam.origin.localScale = Vector3.one * VR.Settings.IPDScale;
        //}

        public void Refresh()
        {
            CopyFX(Blueprint);
        }

        internal Camera Clone(bool copyEffects = true)
        {
            var camera = new GameObject("VRGIN_Camera_Clone").CopyComponentFrom(SteamCam.GetComponent<Camera>());
            if (copyEffects) CopyFX(SteamCam.gameObject, camera.gameObject);
            camera.transform.position = transform.position;
            camera.transform.rotation = transform.rotation;
            camera.nearClipPlane = 0.01f;
            return camera;
        }

        internal void RegisterSlave(CameraSlave slave)
        {
            VRLog.Debug("Camera went online: {0}", slave.name);
            if (!Slaves.Contains(slave))
            {
                Slaves.Add(slave);
            }
            UpdateCameraConfig();
        }

        internal void UnregisterSlave(CameraSlave slave)
        {
            if (Slaves.Remove(slave))
            {
                VRLog.Debug("Camera went offline: {0}", slave.name);
                UpdateCameraConfig();
            }
        }

        public void SyncSkybox()
        {
            if (HasValidBlueprint && (bool)_Camera)
            {
                if (_Blueprint.clearFlags == CameraClearFlags.Skybox && _Camera.clearFlags != CameraClearFlags.Skybox)
                    _Camera.clearFlags = _Blueprint.clearFlags;
                else if (_Blueprint.clearFlags == CameraClearFlags.Color && _Camera.clearFlags != CameraClearFlags.Color) _Camera.clearFlags = _Blueprint.clearFlags;
                if (_Camera.clearFlags == CameraClearFlags.Color) _Camera.backgroundColor = _Blueprint.backgroundColor;
            }
        }
    }
}
