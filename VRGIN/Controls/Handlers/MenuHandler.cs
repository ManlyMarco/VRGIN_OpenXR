using System;
using System.Linq;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls.Tools;
using VRGIN.Core;
using VRGIN.Native;
using VRGIN.Visuals;

namespace VRGIN.Controls.Handlers
{
    public class MenuHandler : ProtectedBehaviour
    {
        private class ResizeHandler : ProtectedBehaviour
        {
            private GUIQuad _Gui;

            private Vector3? _StartLeft;

            private Vector3? _StartRight;

            private Vector3? _StartScale;

            private Quaternion? _StartRotation;

            private Vector3? _StartPosition;

            private Quaternion _StartRotationController;

            private Vector3? _OffsetFromCenter;

            public bool IsDragging { get; private set; }

            protected override void OnStart()
            {
                base.OnStart();
                _Gui = GetComponent<GUIQuad>();
            }

            protected override void OnUpdate()
            {
                base.OnUpdate();
                IsDragging = VR.Mode.Left.Input.GetPress(EVRButtonId.k_EButton_Grip) &&
                             VR.Mode.Right.Input.GetPress(EVRButtonId.k_EButton_Grip);
                if (IsDragging)
                {
                    if (!_StartScale.HasValue) Initialize();
                    var position = VR.Mode.Left.transform.position;
                    var position2 = VR.Mode.Right.transform.position;
                    var num = Vector3.Distance(position, position2);
                    var num2 = Vector3.Distance(_StartLeft.Value, _StartRight.Value);
                    var vector = position2 - position;
                    var vector2 = position + vector * 0.5f;
                    var quaternion = Quaternion.Inverse(VR.Camera.SteamCam.origin.rotation);
                    var averageRotation = GetAverageRotation();
                    var quaternion2 = quaternion * averageRotation * Quaternion.Inverse(quaternion * _StartRotationController);
                    _Gui.transform.localScale = num / num2 * _StartScale.Value;
                    _Gui.transform.localRotation = quaternion2 * _StartRotation.Value;
                    _Gui.transform.position = vector2 + averageRotation * Quaternion.Inverse(_StartRotationController) * _OffsetFromCenter.Value;
                }
                else
                    _StartScale = null;
            }

            private Quaternion GetAverageRotation()
            {
                var position = VR.Mode.Left.transform.position;
                var normalized = (VR.Mode.Right.transform.position - position).normalized;
                var vector = Vector3.Lerp(VR.Mode.Left.transform.forward, VR.Mode.Right.transform.forward, 0.5f);
                return Quaternion.LookRotation(Vector3.Cross(normalized, vector).normalized, vector);
            }

            private void Initialize()
            {
                _StartLeft = VR.Mode.Left.transform.position;
                _StartRight = VR.Mode.Right.transform.position;
                _StartScale = _Gui.transform.localScale;
                _StartRotation = _Gui.transform.localRotation;
                _StartPosition = _Gui.transform.position;
                _StartRotationController = GetAverageRotation();
                Vector3.Distance(_StartLeft.Value, _StartRight.Value);
                var vector = _StartRight.Value - _StartLeft.Value;
                var vector2 = _StartLeft.Value + vector * 0.5f;
                _OffsetFromCenter = transform.position - vector2;
            }
        }

        private Controller _Controller;

        private const float RANGE = 0.25f;

        private const int MOUSE_STABILIZER_THRESHOLD = 30;

        private Controller.Lock _LaserLock = Controller.Lock.Invalid;

        private LineRenderer Laser;

        private Vector2? mouseDownPosition;

        private GUIQuad _Target;

        private MenuHandler _Other;

        private ResizeHandler _ResizeHandler;

        private Vector3 _ScaleVector;
        private Buttons _PressedButtons;
        private Controller.TrackpadDirection _LastDirection;
        private float? _NextScrollTime;

        enum Buttons
        {
            Left = 1,
            Right = 2,
            Middle = 4,
        }

        private bool IsResizing
        {
            get
            {
                if ((bool)_ResizeHandler) return _ResizeHandler.IsDragging;
                return false;
            }
        }

        public bool LaserVisible
        {
            get
            {
                if ((bool)Laser) return Laser.gameObject.activeSelf;
                return false;
            }
            set
            {
                if (!Laser) return;
                if (value && !_LaserLock.IsValid)
                {
                    _LaserLock = _Controller.AcquireFocus();
                    if (!_LaserLock.IsValid) return;
                }
                else if (!value && _LaserLock.IsValid) _LaserLock.Release();

                Laser.gameObject.SetActive(value);
                if (value)
                {
                    Laser.SetPosition(0, Laser.transform.position);
                    Laser.SetPosition(1, Laser.transform.position);
                }
                else
                    mouseDownPosition = null;
            }
        }

        public bool IsPressing => _PressedButtons != 0;

        private static void SetMousePosition(Vector2 newPos)
        {
            int x = (int)Mathf.Round(newPos.x);
            int y = (int)Mathf.Round(newPos.y);
            var clientRect = WindowManager.GetClientRect();
            var virtualScreenRect = WindowManager.GetVirtualScreenRect();
            VR.Input.Mouse.MoveMouseToPositionOnVirtualDesktop(
                (clientRect.Left + x - virtualScreenRect.Left) * 65535.0 / (virtualScreenRect.Right - virtualScreenRect.Left),
                (clientRect.Top + y - virtualScreenRect.Top) * 65535.0 / (virtualScreenRect.Bottom - virtualScreenRect.Top));
        }

        protected override void OnAwake()
        {
            base.OnAwake();
            VRLog.Info("Menu Handler OnAwake");
            _Controller = GetComponent<Controller>();
            _ScaleVector = new Vector2((float)VRGUI.Width / (float)Screen.width, (float)VRGUI.Height / (float)Screen.height);
        }

        protected override void OnStart()
        {
            base.OnStart();
            VRLog.Info("Menu Handler started");
            _Other = _Controller.Other.GetComponent<MenuHandler>();
        }

        private void OnRenderModelLoaded()
        {
            try
            {
                if (!_Controller) _Controller = GetComponent<Controller>();
                var transform = _Controller.FindAttachPosition("tip");
                if (!transform)
                {
                    VRLog.Error("Attach position not found for laser!");
                    transform = this.transform;
                }

                Laser = new GameObject().AddComponent<LineRenderer>();
                Laser.transform.SetParent(transform, false);
                Laser.material = new Material(VR.Context.Materials.Sprite);
                Laser.material.renderQueue += 5000;
                Laser.SetColors(Color.cyan, Color.cyan);
                if (SteamVR.instance.hmd_TrackingSystemName == "lighthouse")
                {
                    Laser.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
                    Laser.transform.position += Laser.transform.forward * 0.06f;
                }

                Laser.SetVertexCount(2);
                Laser.useWorldSpace = true;
                Laser.SetWidth(0.002f, 0.002f);
            }
            catch (Exception obj)
            {
                VRLog.Error(obj);
            }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (LaserVisible)
            {
                if (IsResizing)
                {
                    Laser.SetPosition(0, Laser.transform.position);
                    Laser.SetPosition(1, Laser.transform.position);
                }
                else
                    UpdateLaser();
            }
            else if (_Controller.CanAcquireFocus()) CheckForNearMenu();

            CheckInput();
        }

        private void OnDisable()
        {
            if (_LaserLock.IsValid) _LaserLock.Release();
        }

        private void EnsureResizeHandler()
        {
            if (!_ResizeHandler)
            {
                _ResizeHandler = _Target.GetComponent<ResizeHandler>();
                if (!_ResizeHandler) _ResizeHandler = _Target.gameObject.AddComponent<ResizeHandler>();
            }
        }

        private void EnsureNoResizeHandler()
        {
            if ((bool)_ResizeHandler) DestroyImmediate(_ResizeHandler);
            _ResizeHandler = null;
        }

        protected void CheckInput()
        {
            var input = _Controller.Input;
            if (LaserVisible && _Target)
            {
                if (_Other.LaserVisible && _Other._Target == _Target)
                {
                    // No double input - this is handled by ResizeHandler
                    EnsureResizeHandler();
                }
                else
                {
                    EnsureNoResizeHandler();
                }

                if (input.GetPressDown(EVRButtonId.k_EButton_SteamVR_Trigger))
                {
                    VR.Input.Mouse.LeftButtonDown();
                    _PressedButtons |= Buttons.Left;
                    mouseDownPosition = Vector2.Scale(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y), _ScaleVector);
                }
                if (input.GetPressUp(EVRButtonId.k_EButton_SteamVR_Trigger))
                {
                    _PressedButtons &= ~Buttons.Left;
                    VR.Input.Mouse.LeftButtonUp();
                    mouseDownPosition = null;
                }
                var currentDirection = _Controller.GetTrackpadDirection();
                if (input.GetPressDown(EVRButtonId.k_EButton_SteamVR_Touchpad))
                {
                    _LastDirection = currentDirection;
                    switch (_LastDirection)
                    {
                        case Controller.TrackpadDirection.Right:
                            VR.Input.Mouse.RightButtonDown();
                            _PressedButtons |= Buttons.Right;
                            break;
                        case Controller.TrackpadDirection.Center:
                            VR.Input.Mouse.MiddleButtonDown();
                            _PressedButtons |= Buttons.Middle;
                            break;
                        default:
                            break;
                    }
                }
                if (input.GetPressUp(EVRButtonId.k_EButton_SteamVR_Touchpad))
                {
                    switch (_LastDirection)
                    {
                        case Controller.TrackpadDirection.Right:
                            VR.Input.Mouse.RightButtonUp();
                            _PressedButtons &= ~Buttons.Right;
                            break;
                        case Controller.TrackpadDirection.Center:
                            VR.Input.Mouse.MiddleButtonUp();
                            _PressedButtons &= ~Buttons.Middle;
                            break;
                        default:
                            break;
                    }
                }
                switch (currentDirection)
                {
                    case Controller.TrackpadDirection.Up:
                        Scroll(1);
                        break;
                    case Controller.TrackpadDirection.Down:
                        Scroll(-1);
                        break;
                    default:
                        _NextScrollTime = null;
                        break;
                }

                if (input.GetPressDown(EVRButtonId.k_EButton_Grip) && !_Target.IsOwned)
                {
                    _Target.transform.SetParent(_Controller.transform, true);
                    _Target.IsOwned = true;
                }
                if (input.GetPressUp(EVRButtonId.k_EButton_Grip))
                {
                    AbandonGUI();
                }
            }
        }

        private void Scroll(int amount)
        {
            if (_NextScrollTime == null)
            {
                _NextScrollTime = Time.unscaledTime + 0.5f;
            }
            else if (_NextScrollTime < Time.unscaledTime)
            {
                _NextScrollTime += 0.1f;
            }
            else
            {
                return;
            }
            VR.Input.Mouse.VerticalScroll(amount);
        }

        private void CheckForNearMenu()
        {
            _Target = GUIQuadRegistry.Quads.FirstOrDefault(IsLaserable);
            if ((bool)_Target) LaserVisible = true;
        }

        private bool IsLaserable(GUIQuad quad)
        {
            RaycastHit hit;
            if (IsWithinRange(quad)) return Raycast(quad, out hit);
            return false;
        }

        private float GetRange(GUIQuad quad)
        {
            return Mathf.Clamp(quad.transform.localScale.magnitude * 0.25f, 0.25f, 1.25f) * VR.Settings.IPDScale;
        }

        private bool IsWithinRange(GUIQuad quad)
        {
            if (!Laser) return false;
            if (quad.transform.parent == transform) return false;
            var lhs = -quad.transform.forward;
            _ = quad.transform.position;
            var position = Laser.transform.position;
            var forward = Laser.transform.forward;
            var num = (0f - quad.transform.InverseTransformPoint(position).z) * quad.transform.localScale.magnitude;
            if (num > 0f && num < GetRange(quad)) return Vector3.Dot(lhs, forward) < 0f;
            return false;
        }

        private bool Raycast(GUIQuad quad, out RaycastHit hit)
        {
            var position = Laser.transform.position;
            var forward = Laser.transform.forward;
            var component = quad.GetComponent<Collider>();
            if ((bool)component)
            {
                var ray = new Ray(position, forward);
                return component.Raycast(ray, out hit, GetRange(quad));
            }

            hit = default;
            return false;
        }

        private void UpdateLaser()
        {
            Laser.SetPosition(0, Laser.transform.position);
            Laser.SetPosition(1, Laser.transform.position + Laser.transform.forward);

            if (_Target && _Target.gameObject.activeInHierarchy)
            {
                if (IsWithinRange(_Target) && Raycast(_Target, out var hit))
                {
                    Laser.SetPosition(1, hit.point);
                    if (!IsOtherWorkingOn(_Target))
                    {
                        var newPos = new Vector2(hit.textureCoord.x * VRGUI.Width, (1 - hit.textureCoord.y) * VRGUI.Height);
                        //VRLog.Info("New Pos: {0}, textureCoord: {1}", newPos, hit.textureCoord);
                        if (!mouseDownPosition.HasValue || Vector2.Distance(mouseDownPosition.Value, newPos) > MOUSE_STABILIZER_THRESHOLD)
                        {
                            SetMousePosition(newPos);
                            mouseDownPosition = null;
                        }
                    }
                }
                else
                {
                    // Out of view
                    LaserVisible = false;
                    ClearPresses();
                }
            }
            else
            {
                // May day, may day -- window is gone!
                LaserVisible = false;
                ClearPresses();
            }
        }

        private void ClearPresses()
        {
            AbandonGUI();
            if ((_PressedButtons & Buttons.Left) != 0)
            {
                VR.Input.Mouse.LeftButtonUp();
            }
            if ((_PressedButtons & Buttons.Right) != 0)
            {
                VR.Input.Mouse.RightButtonUp();
            }
            if ((_PressedButtons & Buttons.Middle) != 0)
            {
                VR.Input.Mouse.MiddleButtonUp();
            }
            _PressedButtons = 0;
            _NextScrollTime = null;
        }

        private void AbandonGUI()
        {
            if (_Target && _Target.transform.parent == _Controller.transform)
            {
                _Target.transform.SetParent(VR.Camera.Origin, true);
                _Target.IsOwned = false;
            }
        }

        private bool IsOtherWorkingOn(GUIQuad target)
        {
            if ((bool)_Other && _Other.LaserVisible && _Other._Target == target) return _Other.IsPressing;
            return false;
        }
    }
}