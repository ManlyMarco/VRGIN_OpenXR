using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;
using VRGIN.Helpers;
using VRGIN.Visuals;

namespace VRGIN.Modes
{
    public abstract class ControlMode : ProtectedBehaviour
    {
        private static bool _ControllerFound;

        private static int cnter;

        public abstract ETrackingUniverseOrigin TrackingOrigin { get; }

        public Controller Left { get; private set; }

        public Controller Right { get; private set; }

        protected IEnumerable<IShortcut> Shortcuts { get; private set; }

        public virtual IEnumerable<Type> Tools => new List<Type>();

        public virtual IEnumerable<Type> LeftTools => new List<Type>();

        public virtual IEnumerable<Type> RightTools => new List<Type>();

        internal event EventHandler<EventArgs> ControllersCreated = delegate { };

        public virtual void Impersonate(IActor actor)
        {
            Impersonate(actor, ImpersonationMode.Approximately);
        }

        public virtual void Impersonate(IActor actor, ImpersonationMode mode)
        {
            if (actor != null) actor.HasHead = false;
        }

        public virtual void MoveToPosition(Vector3 targetPosition, bool ignoreHeight = true)
        {
            MoveToPosition(targetPosition, VR.Camera.SteamCam.head.rotation, ignoreHeight);
        }

        public virtual void MoveToPosition(Vector3 targetPosition, Quaternion rotation = default(Quaternion), bool ignoreHeight = true)
        {
            var levelRotation = MakeUpright(rotation) * Quaternion.Inverse(MakeUpright(VR.Camera.SteamCam.head.rotation));
            VR.Camera.SteamCam.origin.rotation = levelRotation * VR.Camera.SteamCam.origin.rotation;

            float targetY = ignoreHeight ? 0 : targetPosition.y;
            float myY = ignoreHeight ? 0 : VR.Camera.SteamCam.head.position.y;
            targetPosition = new Vector3(targetPosition.x, targetY, targetPosition.z);
            var myPosition = new Vector3(VR.Camera.SteamCam.head.position.x, myY, VR.Camera.SteamCam.head.position.z);
            VR.Camera.SteamCam.origin.position += (targetPosition - myPosition);
        }

        private static Quaternion MakeUpright(Quaternion rotation)
        {
            return Quaternion.Euler(0, rotation.eulerAngles.y, 0);
        }

        protected override void OnStart()
        {
            CreateControllers();
            Shortcuts = CreateShortcuts();
            OpenVR.Compositor.SetTrackingSpace(TrackingOrigin);
            InitializeScreenCapture();
        }

        protected virtual void OnEnable()
        {
            SteamVR_Events.DeviceConnected.Listen(OnDeviceConnected);
            VRLog.Info("Enabled {0}", GetType().Name);
        }

        protected virtual void OnDisable()
        {
            VRLog.Info("Disabled {0}", GetType().Name);
            SteamVR_Events.DeviceConnected.Remove(OnDeviceConnected);
        }

        protected virtual void CreateControllers()
        {
            var steamCam = VR.Camera.SteamCam;
            steamCam.origin.gameObject.SetActive(false);
            Left = CreateLeftController();
            Left.transform.SetParent(steamCam.origin, false);
            Right = CreateRightController();
            Right.transform.SetParent(steamCam.origin, false);
            Left.Other = Right;
            Right.Other = Left;
            Left.InputSources = SteamVR_Input_Sources.LeftHand;
            Right.InputSources = SteamVR_Input_Sources.RightHand;
            steamCam.origin.gameObject.SetActive(true);
            VRLog.Info("---- Initialize left tools");
            InitializeTools(Left, true);
            VRLog.Info("---- Initialize right tools");
            InitializeTools(Right, false);
            ControllersCreated(this, new EventArgs());
            DontDestroyOnLoad(steamCam.origin.gameObject);
        }

        public virtual void OnDestroy()
        {
            VRLog.Debug("ControlMode OnDestroy called.");
            Destroy(Left);
            Destroy(Right);
            if (Shortcuts == null) return;
            foreach (var shortcut in Shortcuts) shortcut.Dispose();
        }

        protected virtual void InitializeTools(Controller controller, bool isLeft)
        {
            // Combine
            var toolTypes = Tools.Concat(isLeft ? LeftTools : RightTools).Distinct();

            foreach (var type in toolTypes)
            {
                controller.AddTool(type);
            }

            VRLog.Info("{0} tools added", toolTypes.Count());
        }

        protected virtual Controller CreateLeftController()
        {
            return LeftController.Create();
        }

        protected virtual Controller CreateRightController()
        {
            return RightController.Create();
        }

        protected virtual IEnumerable<IShortcut> CreateShortcuts()
        {
            return new List<IShortcut>
            {
                new KeyboardShortcut(VR.Shortcuts.ShrinkWorld, delegate { VR.Settings.IPDScale += Time.deltaTime; }),
                new KeyboardShortcut(VR.Shortcuts.EnlargeWorld, delegate { VR.Settings.IPDScale -= Time.deltaTime; }),
                new MultiKeyboardShortcut(new KeyStroke("Ctrl + C"), new KeyStroke("Ctrl + D"), delegate { UnityHelper.DumpScene("dump.json"); }),
                new MultiKeyboardShortcut(new KeyStroke("Ctrl + C"), new KeyStroke("Ctrl + I"), delegate { UnityHelper.DumpScene("dump.json", true); }),
                new MultiKeyboardShortcut(VR.Shortcuts.ToggleUserCamera, ToggleUserCamera),
                new MultiKeyboardShortcut(VR.Shortcuts.SaveSettings, delegate { VR.Settings.Save(); }),
                new KeyboardShortcut(VR.Shortcuts.LoadSettings, delegate { VR.Settings.Reload(); }),
                new KeyboardShortcut(VR.Shortcuts.ResetSettings, delegate { VR.Settings.Reset(); }),
                new KeyboardShortcut(VR.Shortcuts.ApplyEffects, delegate { VR.Manager.ToggleEffects(); })
            };
        }

        protected virtual void ToggleUserCamera()
        {
            if (!PlayerCamera.Created)
            {
                VRLog.Info("Create user camera");
                PlayerCamera.Create();
            }
            else
            {
                VRLog.Info("Remove user camera");
                PlayerCamera.Remove();
            }
        }

        protected virtual void InitializeScreenCapture() { }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            OpenVR.Compositor.SetTrackingSpace(TrackingOrigin);

            // Update head visibility

            var steamCam = VRCamera.Instance.SteamCam;
            int i = 0;

            bool allActorsHaveHeads = VR.Interpreter.IsEveryoneHeaded;

            foreach (var actor in VR.Interpreter.Actors)
            {
                if (actor.HasHead)
                {
                    if (allActorsHaveHeads)
                    {
                        var hisPos = actor.Eyes.position;
                        var hisForward = actor.Eyes.forward;

                        var myPos = steamCam.head.position;
                        var myForward = steamCam.head.forward;

                        VRLog.Debug("Actor #{0} -- He: {1} -> {2} | Me: {3} -> {4}", i, hisPos, hisForward, myPos, myForward);
                        if (Vector3.Distance(hisPos, myPos) * VR.Context.UnitToMeter <  0.15f && Vector3.Dot(hisForward, myForward) > 0.6f)
                        {
                            actor.HasHead = false;
                        }
                    }
                }
                else
                {
                    if (Vector3.Distance(actor.Eyes.position, steamCam.head.position) * VR.Context.UnitToMeter > 0.3f)
                    {
                        actor.HasHead = true;
                    }
                }
                i++;
            }

            CheckInput();
        }

        protected void CheckInput()
        {
            //foreach (var shortcut in Shortcuts) shortcut.Evaluate();
        }

        private void OnDeviceConnected(int idx, bool connected)
        {
            if (_ControllerFound) return;
            VRLog.Info("Device connected: {0}", (uint)idx);
            if (connected && idx != 0)
            {
                var system = OpenVR.System;
                if (system != null && system.GetTrackedDeviceClass((uint)idx) == ETrackedDeviceClass.Controller)
                {
                    _ControllerFound = true;
                    ChangeModeOnControllersDetected();
                }
            }
        }

        protected virtual void ChangeModeOnControllersDetected() { }
    }
}
