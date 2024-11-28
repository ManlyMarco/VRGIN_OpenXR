using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;
using VRGIN.Helpers;
using VRGIN.Native;
using VRGIN.Visuals;

namespace VRGIN.Controls.Tools
{
    public class MenuTool : Tool
    {
        private float pressDownTime;

        private Vector2 touchDownPosition;

        private WindowsInterop.POINT touchDownMousePosition;

        private float timeAbandoned;

        private double _DeltaX;

        private double _DeltaY;

        public GUIQuad Gui { get; private set; }

        public override Texture2D Image => UnityHelper.LoadImage("icon_settings.png");

        public void TakeGUI(GUIQuad quad)
        {
            if ((bool)quad && !Gui && !quad.IsOwned)
            {
                Gui = quad;
                Gui.transform.parent = transform;
                Gui.transform.SetParent(transform, true);
                Gui.transform.localPosition = new Vector3(0f, 0.05f, -0.06f);
                Gui.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                quad.IsOwned = true;
            }
        }

        public void AbandonGUI()
        {
            if ((bool)Gui)
            {
                timeAbandoned = Time.unscaledTime;
                Gui.IsOwned = false;
                Gui.transform.SetParent(VR.Camera.SteamCam.origin, true);
                Gui = null;
            }
        }

        protected override void OnAwake()
        {
            base.OnAwake();
            Gui = GUIQuad.Create(null);
            Gui.transform.parent = transform;
            Gui.transform.localScale = Vector3.one * 0.3f;
            Gui.transform.localPosition = new Vector3(0f, 0.05f, -0.06f);
            Gui.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Gui.IsOwned = true;
            DontDestroyOnLoad(Gui.gameObject);
            Gui.gameObject.SetActive(enabled);
        }

        protected override void OnStart()
        {
            base.OnStart();
        }

        protected override void OnDestroy()
        {
            if (VR.Quitting)
            {
                return;
            }
            DestroyImmediate(Gui.gameObject);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (Gui)
            {
                Gui.gameObject.SetActive(false);
            }

            if (pressDownTime != 0)
            {
                pressDownTime = 0;
                VR.Input.Mouse.LeftButtonUp();
            }
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            if ((bool)Gui) Gui.gameObject.SetActive(true);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            var device = this.Controller;

            if (device.GetPressDown(ButtonMask.Touchpad | ButtonMask.Trigger))
            {
                VR.Input.Mouse.LeftButtonDown();
                pressDownTime = Time.unscaledTime;
            }

            if (device.GetPressUp(ButtonMask.Grip))
            {
                if (Gui)
                {
                    AbandonGUI();
                }
                else
                {
                    TakeGUI(GUIQuadRegistry.Quads.FirstOrDefault(q => !q.IsOwned));
                }
            }

            if (device.GetTouchDown(ButtonMask.Touchpad))
            {
                touchDownPosition = device.GetAxis();
                touchDownMousePosition = MouseOperations.GetClientCursorPosition();
            }
            if (device.GetTouch(ButtonMask.Touchpad) && (Time.unscaledTime - pressDownTime) > 0.3f)
            {
                var pos = device.GetAxis();
                var diff = pos - (VR.HMD == HMDType.Oculus ? Vector2.zero : touchDownPosition);
                var factor = VR.HMD == HMDType.Oculus ? Time.unscaledDeltaTime * 5 : 1f;
                // We can only move by integral number of pixels, so accumulate them until we have an integral value
                _DeltaX += (diff.x * VRGUI.Width * 0.1 * factor);
                _DeltaY += (-diff.y * VRGUI.Height * 0.2 * factor);

                int deltaX = (int)(_DeltaX > 0 ? Math.Floor(_DeltaX) : Math.Ceiling(_DeltaX));
                int deltaY = (int)(_DeltaY > 0 ? Math.Floor(_DeltaY) : Math.Ceiling(_DeltaY));

                _DeltaX -= deltaX;
                _DeltaY -= deltaY;

                MoveMouseWithinWindow(deltaX, deltaY);
                touchDownPosition = pos;
            }

            if (device.GetPressUp(ButtonMask.Touchpad | ButtonMask.Trigger))
            {
                VR.Input.Mouse.LeftButtonUp();
                pressDownTime = 0;
            }
        }
        private static void MoveMouseWithinWindow(int deltaX, int deltaY)
        {
            var clientRect = WindowManager.GetClientRect();
            var virtualScreenRect = WindowManager.GetVirtualScreenRect();
            var current = MouseOperations.GetCursorPosition();
            var x = Mathf.Clamp(current.X + deltaX, clientRect.Left, clientRect.Right - 1);
            var y = Mathf.Clamp(current.Y + deltaY, clientRect.Top, clientRect.Bottom - 1);
            VR.Input.Mouse.MoveMouseToPositionOnVirtualDesktop(
                (x - virtualScreenRect.Left) * 65535.0 / (virtualScreenRect.Right - virtualScreenRect.Left),
                (y - virtualScreenRect.Top) * 65535.0 / (virtualScreenRect.Bottom - virtualScreenRect.Top));
        }

        public override List<HelpText> GetHelpTexts()
        {
            return new List<HelpText>(new HelpText[3]
            {
                HelpText.Create("Tap to click", FindAttachPosition("trackpad"), new Vector3(0f, 0.02f, 0.05f)),
                HelpText.Create("Slide to move cursor", FindAttachPosition("trackpad"), new Vector3(0.05f, 0.02f, 0f), new Vector3(0.015f, 0f, 0f)),
                HelpText.Create("Attach/Remove menu", FindAttachPosition("lgrip"), new Vector3(-0.06f, 0f, -0.05f))
            });
        }
    }
}
