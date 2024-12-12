using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Controls.Tools;
using VRGIN.Core;
using VRGIN.Native;
using VRGIN.Visuals;
using static VRGIN.Native.WindowsInterop;

namespace VRGIN.Controls.Handlers
{
    /// <summary>
    /// Handler that is in charge of the menu interaction with controllers
    /// </summary>
    public class MenuHandlerGame : ProtectedBehaviour
    {
        private Controller _Controller;

        //const float RANGE = 0.25f;

        private const int MOUSE_STABILIZER_THRESHOLD = 30; // pixels

        private Controller.Lock _LaserLock = Controller.Lock.Invalid;

        private LineRenderer Laser;

        private Vector2? mouseDownPosition;

        private GUIQuad _Target;

        MenuHandlerGame _Other;

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

        protected override void OnStart()
        {
            base.OnStart();
            _Controller = GetComponent<Controller>();
            _Other = _Controller.Other.GetComponent<MenuHandlerGame>();
            _ScaleVector = new Vector2((float)VRGUI.Width / Screen.width, (float)VRGUI.Height / Screen.height);
        }

        private void OnRenderModelLoaded()  
        {
            try
            {
                if (!_Controller)
                    _Controller = GetComponent<Controller>();
                var attachPosition = _Controller.FindAttachPosition("tip");

                if (!attachPosition)
                {
                    VRLog.Error("Attach position not found for laser!");
                    attachPosition = transform;
                }   
                Laser = new GameObject("Laser").AddComponent<LineRenderer>();
                Laser.transform.parent = _Controller.transform;
                Laser.transform.SetPositionAndRotation(attachPosition.position, attachPosition.rotation);
                Laser.material = new Material(Shader.Find("Sprites/Default"));
                Laser.material.renderQueue += 5000;
                Laser.SetColors(Color.cyan, Color.cyan);

                if (SteamVR.instance.hmd_TrackingSystemName == "lighthouse")
                {
                    Laser.transform.localRotation = Quaternion.Euler(60, 0, 0);
                    Laser.transform.position += Laser.transform.forward * 0.06f;
                }
                else
                {
                    Laser.transform.localRotation *= Quaternion.Euler(30f, 0, 0);
                }
                Laser.SetVertexCount(2);
                Laser.useWorldSpace = true;
                Laser.SetWidth(0.002f, 0.002f);
            }
            catch (Exception e)
            {
                VRLog.Error(e);
            }
        }
        protected override void OnUpdate()
        {
            if (LaserVisible)
            {
                UpdateLaser();
                CheckInput();
            }
            else if (_Controller.CanAcquireFocus() && !_Controller.Input.GetPress(EVRButtonId.k_EButton_Grip))
            {
                CheckForNearMenu();
            }

        }

        private void OnDisable()
        {
            if (_LaserLock.IsValid)
            {
                // Release to be sure
                _LaserLock.Release();
            }
        }

        private bool _triggerPressed;
        protected void CheckInput()
        {
            if (LaserVisible && _Target)
            {
                //if (_Other.LaserVisible && _Other._Target == _Target)
                //{
                //    // No double input - this is handled by ResizeHandler
                //    EnsureResizeHandler();
                //}
                //else
                //{
                //    EnsureNoResizeHandler();
                //}

                if (_Controller.Input.GetPressDown(EVRButtonId.k_EButton_SteamVR_Trigger))
                {
                    _triggerPressed = true;
                    VR.Input.Mouse.LeftButtonDown();
                    _PressedButtons |= Buttons.Left;
                    mouseDownPosition = Vector2.Scale(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y), _ScaleVector);
                }
                else if (_Controller.Input.GetPressUp(EVRButtonId.k_EButton_SteamVR_Trigger))
                {
                    _triggerPressed = false;
                    _PressedButtons &= ~Buttons.Left;
                    VR.Input.Mouse.LeftButtonUp();
                    mouseDownPosition = null;
                }
                var currentDirection = _Controller.GetTrackpadDirection();
                if (_Controller.Input.GetPressDown(EVRButtonId.k_EButton_SteamVR_Touchpad))
                {
                    if (_Target.IsOwned && _triggerPressed)
                    {
                        _Target.transform.SetParent(VR.Manager.transform, true);
                        _Target.IsOwned = false;
                    }
                    else
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

                }
                else if (_Controller.Input.GetPressUp(EVRButtonId.k_EButton_SteamVR_Touchpad))
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
                        if (_Target.IsOwned)
                        {
                            if (_triggerPressed)
                                MoveGui(Time.deltaTime);
                            else
                                ChangeGuiSize(Time.deltaTime);
                        }
                        else
                        {
                            Scroll(1);
                        }
                        break;
                    case Controller.TrackpadDirection.Down:
                        if (_Target.IsOwned)
                        {
                            if (_triggerPressed)
                                MoveGui(-Time.deltaTime);
                            else
                                ChangeGuiSize(-Time.deltaTime);
                        }
                        else
                        {
                            Scroll(-1);
                        }
                        break;
                    default:
                        _NextScrollTime = null;
                        break;
                }

                if (_Controller.Input.GetPressDown(EVRButtonId.k_EButton_Grip) && !_Target.IsOwned)
                {
                    _Target.transform.SetParent(_Controller.transform, worldPositionStays: true);
                    _Target.IsOwned = true;
                }
                else if (_Controller.Input.GetPressUp(EVRButtonId.k_EButton_Grip))
                {
                    AbandonGUI();
                }
                //UpdateHelp();
            }
        }
        private void ChangeGuiSize(float number)
        {
            _Target.transform.localScale *= 1 + number;
        }
        private void MoveGui(float number)
        {
            _Target.transform.position += number * 0.6f * Laser.transform.forward;
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
        void CheckForNearMenu()
        {
            _Target = GUIQuadRegistry.Quads.FirstOrDefault(IsLaserable);
            if (_Target)
            {
                LaserVisible = true;
            }
        }

        bool IsLaserable(GUIQuad quad)
        {
            return IsWithinRange(quad) && Raycast(quad, out _);
        }

        float GetRange(GUIQuad quad)
        {
            return quad.transform.localScale.z;
            //return Mathf.Clamp(quad.transform.localScale.magnitude * RANGE, RANGE, RANGE * 5) * VR.Settings.IPDScale;
        }
        bool IsWithinRange(GUIQuad quad)
        {
            if (Laser)
            {
                return (quad.transform.position - Laser.transform.position).magnitude < GetRange(quad);
            }
            return false;
        }

        bool Raycast(GUIQuad quad, out RaycastHit hit)
        {
            return quad.GetComponent<Collider>().Raycast(new Ray(Laser.transform.position, Laser.transform.forward), out hit, GetRange(quad));
        }
        void UpdateLaser()
        {
            if (_Target
                && _Target.gameObject.activeInHierarchy
                && IsWithinRange(_Target)
                && Raycast(_Target, out var hit))
            {
                Laser.SetPosition(0, Laser.transform.position);
                Laser.SetPosition(1, hit.point);
                if (!IsOtherWorkingOn(_Target))
                {
                    var newPos = new Vector2(hit.textureCoord.x * VRGUI.Width, (1 - hit.textureCoord.y) * VRGUI.Height);
                    if (!mouseDownPosition.HasValue || Vector2.Distance(mouseDownPosition.Value, newPos) > MOUSE_STABILIZER_THRESHOLD)
                    {
                        SetMousePosition(newPos);
                        mouseDownPosition = null;
                    }
                }
            }
            else
            {
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
            return _Other && _Other.LaserVisible && _Other._Target == target && _Other.IsPressing;
        }

        public bool LaserVisible
        {
            get
            {
                return Laser && Laser.gameObject.activeSelf;
            }
            set
            {
                if (!Laser) return;

                if (value && !_LaserLock.IsValid)
                {
                    // Need to acquire focus!
                    _LaserLock = _Controller.AcquireFocus();
                    if (!_LaserLock.IsValid)
                    {
                        // Could not get focus, do nothing.
                        return;
                    }
                }
                else if (!value && _LaserLock.IsValid)
                {
                    // Need to release focus!
                    _LaserLock.Release();
                }

                // Toggle laser
                Laser.gameObject.SetActive(value);

                // Initialize start position
                if (value)
                {
                    Laser.SetPosition(0, Laser.transform.position);
                    Laser.SetPosition(1, Laser.transform.position);
                }
                else
                {
                    mouseDownPosition = null;
                }
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
    }
}