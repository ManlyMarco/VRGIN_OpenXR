using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;
using VRGIN.Modes;
using WindowsInput;

namespace VRGIN.Core
{
    public class VRManager : ProtectedBehaviour
    {
        private VRGUI _Gui;

        private bool _CameraLoaded;

        private bool _IsEnabledEffects;

        private static VRManager _Instance;

        private HashSet<Camera> _CheckedCameras = new HashSet<Camera>();

        private static Type ModeType;

        public static VRManager Instance
        {
            get
            {
                if (_Instance == null) throw new InvalidOperationException("VR Manager has not been created yet!");
                return _Instance;
            }
        }

        public IVRManagerContext Context { get; private set; }

        public GameInterpreter Interpreter { get; private set; }

        public HMDType HMD { get; private set; }

        public ControlMode Mode { get; private set; }

        public IInputSimulator Input { get; internal set; }

        public event EventHandler<ModeInitializedEventArgs> ModeInitialized = delegate { };

        public static VRManager Create<T>(IVRManagerContext context) where T : GameInterpreter
        {
            if (_Instance == null)
            {
                VR.Active = true;
                _Instance = new GameObject("VRGIN_Manager").AddComponent<VRManager>();
                DontDestroyOnLoad(_Instance.gameObject);
                _Instance.Context = context;
                _Instance.Interpreter = _Instance.gameObject.AddComponent<T>();
                _Instance._Gui = VRGUI.Instance;
                _Instance.Input = new InputSimulator();
                if (VR.Settings.ApplyEffects) _Instance.EnableEffects();
                VR.Settings.Save();
            }

            return _Instance;
        }

        public void SetMode<T>() where T : ControlMode
        {
            if (Mode == null || !(Mode is T))
            {
                ModeType = typeof(T);
                if (Mode != null)
                {
                    VRLog.Debug($"Mode changed {typeof(T)}");
                    Mode.ControllersCreated -= OnControllersCreated;
                    DestroyImmediate(Mode);
                }

                Mode = VRCamera.Instance.gameObject.AddComponent<T>();
                Mode.ControllersCreated += OnControllersCreated;
            }
        }

        protected override void OnAwake()
        {
            var trackingSystemName = SteamVR.instance.hmd_TrackingSystemName;

            VRLog.Info("------------------------------------");
            VRLog.Info(" Booting VR [{0}]", trackingSystemName);
            VRLog.Info("------------------------------------");

            HMD = trackingSystemName == "oculus" ? HMDType.Oculus : trackingSystemName == "lighthouse" ? HMDType.Vive : HMDType.Other;
            
            Application.targetFrameRate = 90;
            Time.fixedDeltaTime = 1f / SteamVR.instance.hmd_DisplayFrequency;
            Application.runInBackground = true;

            DontDestroyOnLoad(SteamVR_Render.instance.gameObject);
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                _CheckedCameras.Clear();
            VRLog.Info($"SceneLoaded:{scene.name}:{mode}");
        }

        // A scratch-pad buffer to keep OnUpdate allocation-free.
        private Camera[] _cameraBuffer = Array.Empty<Camera>();
        protected override void OnUpdate()
        {
            int numCameras = Camera.allCamerasCount;
            if (_cameraBuffer.Length < numCameras)
            {
                _cameraBuffer = new Camera[numCameras];
            }
            Camera.GetAllCameras(_cameraBuffer);
            for(int i = 0; i < numCameras; i++)
            {
                var camera = _cameraBuffer[i];
                _cameraBuffer[i] = null;
                if (_CheckedCameras.Contains(camera))
                {
                    continue;
                }
                _CheckedCameras.Add(camera);
                var judgement = VR.Interpreter.JudgeCamera(camera);
                VRLog.Info("Detected new camera {0} Action: {1}", camera.name, judgement);
                switch (judgement)
                {
                    case CameraJudgement.MainCamera:
                        VR.Camera.Copy(camera, true);
                        if (_IsEnabledEffects) ApplyEffects();
                        break;
                    case CameraJudgement.SubCamera:
                        VR.Camera.Copy(camera);
                        break;
                    case CameraJudgement.GUI:
                        VR.GUI.AddCamera(camera);
                        break;
                    case CameraJudgement.GUIAndCamera:
                        VR.Camera.Copy(camera, false, true);
                        VR.GUI.AddCamera(camera);
                        break;
                }
            }
        }

        private void OnApplicationQuit()
        {
            VR.Quitting = true;
        }

        private void OnControllersCreated(object sender, EventArgs e)
        {
            ModeInitialized(this, new ModeInitializedEventArgs(Mode));
        }

        public void EnableEffects()
        {
            _IsEnabledEffects = true;
            if ((bool)VR.Camera.Blueprint) ApplyEffects();
        }

        public void DisableEffects()
        {
            _IsEnabledEffects = false;
        }

        public void ToggleEffects()
        {
            if (_IsEnabledEffects)
                DisableEffects();
            else
                EnableEffects();
        }

        private void ApplyEffects()
        {
            VR.Camera.CopyFX(VR.Camera.Blueprint);
        }
    }
}
