using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;
using VRGIN.Helpers;
using VRGIN.Modes;
using VRGIN.U46.Visuals;
using VRGIN.Visuals;

namespace VRGIN.Controls.Tools
{
    public class WarpTool : Tool
    {
        private enum WarpState
        {
            None = 0,
            Rotating = 1,
            Transforming = 2,
            Grabbing = 3
        }

        private ArcRenderer ArcRenderer;

        private PlayAreaVisualization _Visualization;

        private PlayArea _ProspectedPlayArea = new PlayArea();

        private const float SCALE_THRESHOLD = 0.05f;

        private const float TRANSLATE_THRESHOLD = 0.05f;

        private WarpState State;

        private TravelDistanceRumble _TravelRumble;

        private Vector3 _PrevPoint;

        private float? _GripStartTime;

        private float? _TriggerDownTime;

        private bool Showing;

        private List<Vector2> _Points = new List<Vector2>();

        private const float GRIP_TIME_THRESHOLD = 0.1f;

        private const float GRIP_DIFF_THRESHOLD = 0.01f;

        private const float EXACT_IMPERSONATION_TIME = 1f;

        private Vector3 _PrevControllerPos;

        private Quaternion _PrevControllerRot;

        private Controller.Lock _OtherLock;

        private float _InitialControllerDistance;

        private float _InitialIPD;

        private Vector3 _PrevFromTo;

        private const EVRButtonId SECONDARY_SCALE_BUTTON = EVRButtonId.k_EButton_Axis1;

        private const EVRButtonId SECONDARY_ROTATE_BUTTON = EVRButtonId.k_EButton_Grip;

        private float _IPDOnStart;

        private bool _ScaleInitialized;

        private bool _RotationInitialized;

        public override Texture2D Image => UnityHelper.LoadImage("icon_warp.png");

        protected override void OnAwake()
        {
            base.OnAwake();
            ArcRenderer = new GameObject("Arc Renderer").AddComponent<ArcRenderer>();
            ArcRenderer.transform.SetParent(transform, false);
            ArcRenderer.gameObject.SetActive(false);
            _TravelRumble = new TravelDistanceRumble(500, 0.1f, transform);
            _TravelRumble.UseLocalPosition = true;
            _Visualization = PlayAreaVisualization.Create(_ProspectedPlayArea);
            DontDestroyOnLoad(_Visualization.gameObject);
            SetVisibility(false);
        }

        protected override void OnDestroy()
        {
            if (VR.Quitting)
            {
                return;
            }
            VRLog.Info("Destroy!");
            DestroyImmediate(_Visualization.gameObject);
        }

        protected override void OnStart()
        {
            VRLog.Info("Start!");
            base.OnStart();
            _IPDOnStart = VR.Settings.IPDScale;
            ResetPlayArea(_ProspectedPlayArea);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetVisibility(false);
            ResetPlayArea(_ProspectedPlayArea);
        }

        public void OnPlayAreaUpdated()
        {
            ResetPlayArea(_ProspectedPlayArea);
        }

        private void SetVisibility(bool visible)
        {
            Showing = visible;
            if (visible)
            {
                ArcRenderer.Update();
                UpdateProspectedArea();
                _Visualization.UpdatePosition();
            }

            ArcRenderer.gameObject.SetActive(visible);
            _Visualization.gameObject.SetActive(visible);
        }

        private void ResetPlayArea(PlayArea area)
        {
            area.Position = VR.Camera.SteamCam.origin.position;
            area.Scale = VR.Settings.IPDScale;
            area.Rotation = VR.Camera.SteamCam.origin.rotation.eulerAngles.y;
        }

        protected override void OnDisable()
        {
            if (VR.Quitting)
            {
                return;
            }
            base.OnDisable();
            EnterState(WarpState.None);
            SetVisibility(false);
            if ((bool)Owner) Owner.StopRumble(_TravelRumble);
        }

        protected override void OnLateUpdate()
        {
            base.OnLateUpdate();
            if (Showing) UpdateProspectedArea();
        }

        private void UpdateProspectedArea()
        {
            ArcRenderer.Offset = _ProspectedPlayArea.Height;
            ArcRenderer.Scale = VR.Settings.IPDScale;
            if (ArcRenderer.Target is Vector3 target)
            {
                _ProspectedPlayArea.Position = new Vector3(target.x, _ProspectedPlayArea.Position.y, target.z);
            }
        }

        private void CheckRotationalPress()
        {
            if (Controller.GetPressDown(EVRButtonId.k_EButton_Axis0))
            {
                var axis = Controller.GetAxis(EVRButtonId.k_EButton_Axis0);
                _ProspectedPlayArea.Reset();
                if (axis.x < -0.2f)
                    _ProspectedPlayArea.Rotation -= 20f;
                else if (axis.x > 0.2f) _ProspectedPlayArea.Rotation += 20f;
                _ProspectedPlayArea.Apply();
            }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (State == WarpState.None)
            {
                if (Controller.GetAxis(EVRButtonId.k_EButton_Axis0).magnitude < 0.5f)
                {
                    if (Controller.GetTouchDown(EVRButtonId.k_EButton_Axis0)) EnterState(WarpState.Rotating);
                }
                else
                    CheckRotationalPress();

                if (Controller.GetPressDown(EVRButtonId.k_EButton_Grip)) EnterState(WarpState.Grabbing);
            }

            if (State == WarpState.Grabbing) HandleGrabbing();
            if (State == WarpState.Rotating) HandleRotation();
            if (State == WarpState.Transforming && Controller.GetPressUp(EVRButtonId.k_EButton_Axis0))
            {
                _ProspectedPlayArea.Apply();
                ArcRenderer.Update();
                EnterState(WarpState.Rotating);
            }

            if (State != 0) return;
            if (Controller.GetHairTriggerDown()) _TriggerDownTime = Time.unscaledTime;
            if (_TriggerDownTime.HasValue)
            {
                if (Controller.GetHairTrigger() && Time.unscaledTime - _TriggerDownTime > 1f)
                {
                    VRManager.Instance.Mode.Impersonate(VR.Interpreter.FindNextActorToImpersonate(), ImpersonationMode.Exactly);
                    _TriggerDownTime = null;
                }

                if (VRManager.Instance.Interpreter.Actors.Any() && Controller.GetHairTriggerUp())
                    VRManager.Instance.Mode.Impersonate(VR.Interpreter.FindNextActorToImpersonate(), ImpersonationMode.Approximately);
            }
        }


        private void HandleRotation()
        {
            if (Showing)
            {
                _Points.Add(Controller.GetAxis(EVRButtonId.k_EButton_Axis0));
                if (_Points.Count > 2) DetectCircle();
            }

            if (Controller.GetPressDown(EVRButtonId.k_EButton_Axis0)) EnterState(WarpState.Transforming);
            if (Controller.GetTouchUp(EVRButtonId.k_EButton_Axis0)) EnterState(WarpState.None);
        }

        private void InitializeScaleIfNeeded()
        {
            if (!_ScaleInitialized)
            {
                _InitialControllerDistance = Vector3.Distance(OtherController.transform.position, transform.position);
                _InitialIPD = VR.Settings.IPDScale;
                _PrevFromTo = (OtherController.transform.position - transform.position).normalized;
                _ScaleInitialized = true;
            }
        }
        private void InitializeRotationIfNeeded()
        {
            if (!_ScaleInitialized && !_RotationInitialized)
            {
                _PrevFromTo = (OtherController.transform.position - transform.position).normalized;
                _RotationInitialized = true;
            }
        }

        private void HandleGrabbing()
        {
            if (OtherController.IsTracking && !HasLock()) OtherController.TryAcquireFocus(out _OtherLock);
            if (HasLock() && OtherController.Input.GetPressDown(EVRButtonId.k_EButton_Axis1)) _ScaleInitialized = false;
            if (HasLock() && OtherController.Input.GetPressDown(EVRButtonId.k_EButton_Grip)) _RotationInitialized = false;
            if (Controller.GetPress(EVRButtonId.k_EButton_Grip))
            {
                if (HasLock() && (OtherController.Input.GetPress(EVRButtonId.k_EButton_Grip) || OtherController.Input.GetPress(EVRButtonId.k_EButton_Axis1)))
                {
                    var normalized = (OtherController.transform.position - transform.position).normalized;
                    if (OtherController.Input.GetPress(EVRButtonId.k_EButton_Axis1))
                    {
                        InitializeScaleIfNeeded();
                        var num = Vector3.Distance(OtherController.transform.position, transform.position) * (_InitialIPD / VR.Settings.IPDScale) / _InitialControllerDistance;
                        VR.Settings.IPDScale = num * _InitialIPD;
                        _ProspectedPlayArea.Scale = VR.Settings.IPDScale;
                    }

                    if (OtherController.Input.GetPress(EVRButtonId.k_EButton_Grip))
                    {
                        InitializeRotationIfNeeded();
                        var num2 = Calculator.Angle(_PrevFromTo, normalized) * VR.Settings.RotationMultiplier;
                        VR.Camera.SteamCam.origin.transform.RotateAround(VR.Camera.Head.position, Vector3.up, num2);
                        _ProspectedPlayArea.Rotation += num2;
                    }

                    _PrevFromTo = (OtherController.transform.position - transform.position).normalized;
                }
                else
                {
                    var vector = transform.position - _PrevControllerPos;
                    var quaternion = Quaternion.Inverse(_PrevControllerRot * Quaternion.Inverse(transform.rotation)) * (transform.rotation * Quaternion.Inverse(transform.rotation));
                    if (Time.unscaledTime - _GripStartTime > 0.1f || Calculator.Distance(vector.magnitude) > 0.01f)
                    {
                        var forward = Vector3.forward;
                        var v = quaternion * Vector3.forward;
                        var num3 = Calculator.Angle(forward, v) * VR.Settings.RotationMultiplier;
                        VR.Camera.SteamCam.origin.transform.position -= vector;
                        _ProspectedPlayArea.Height -= vector.y;
                        if (!VR.Settings.GrabRotationImmediateMode && Controller.GetPress(12884901888uL))
                        {
                            VR.Camera.SteamCam.origin.transform.RotateAround(VR.Camera.Head.position, Vector3.up, 0f - num3);
                            _ProspectedPlayArea.Rotation -= num3;
                        }

                        _GripStartTime = 0f;
                    }
                }
            }

            if (Controller.GetPressUp(EVRButtonId.k_EButton_Grip))
            {
                EnterState(WarpState.None);
                if (Time.unscaledTime - _GripStartTime < 0.1f)
                {
                    Owner.StartRumble(new RumbleImpulse(800));
                    _ProspectedPlayArea.Height = 0f;
                    _ProspectedPlayArea.Scale = _IPDOnStart;
                }
            }

            if (VR.Settings.GrabRotationImmediateMode && Controller.GetPressUp(12884901888uL))
            {
                var normalized2 = Vector3.ProjectOnPlane(transform.position - VR.Camera.Head.position, Vector3.up).normalized;
                var normalized3 = Vector3.ProjectOnPlane(VR.Camera.Head.forward, Vector3.up).normalized;
                var num4 = Calculator.Angle(normalized2, normalized3);
                VR.Camera.SteamCam.origin.transform.RotateAround(VR.Camera.Head.position, Vector3.up, num4);
                _ProspectedPlayArea.Rotation = num4;
            }

            _PrevControllerPos = transform.position;
            _PrevControllerRot = transform.rotation;
            CheckRotationalPress();
        }
        private float NormalizeAngle(float angle)
        {
            return angle % 360f;
        }
        private void DetectCircle()
        {
            float? num = null;
            float? num2 = null;
            var num3 = 0f;
            foreach (var point in _Points)
            {
                var magnitude = point.magnitude;
                num = Math.Max(num ?? magnitude, magnitude);
                num2 = Math.Max(num2 ?? magnitude, magnitude);
                num3 += magnitude;
            }

            num3 /= (float)_Points.Count;
            if (num2 - num < 0.2f && num > 0.2f)
            {
                var num4 = Mathf.Atan2(_Points.First().y, _Points.First().x) * 57.29578f;
                var num5 = Mathf.Atan2(_Points.Last().y, _Points.Last().x) * 57.29578f - num4;
                if (Mathf.Abs(num5) < 60f)
                    _ProspectedPlayArea.Rotation -= num5;
                else
                    VRLog.Info("Discarding too large rotation: {0}", num5);
            }

            _Points.Clear();
        }

        private void EnterState(WarpState state)
        {
            switch (State)
            {
                case WarpState.Grabbing:
                    Owner.StopRumble(_TravelRumble);
                    _ScaleInitialized = _RotationInitialized = false;
                    if (HasLock())
                    {
                        VRLog.Info("Releasing lock on other controller!");
                        _OtherLock.SafeRelease();
                    }

                    break;
            }

            switch (state)
            {
                case WarpState.None:
                    SetVisibility(false);
                    break;
                case WarpState.Rotating:
                    SetVisibility(true);
                    Reset();
                    break;
                case WarpState.Grabbing:
                    _PrevControllerPos = transform.position;
                    _GripStartTime = Time.unscaledTime;
                    _TravelRumble.Reset();
                    _PrevControllerPos = transform.position;
                    _PrevControllerRot = transform.rotation;
                    Owner.StartRumble(_TravelRumble);
                    break;
            }

            State = state;
        }

        private bool HasLock()
        {
            if (_OtherLock != null) return _OtherLock.IsValid;
            return false;
        }

        private void Reset()
        {
            _Points.Clear();
        }

        public override List<HelpText> GetHelpTexts()
        {
            return new List<HelpText>(new HelpText[5]
            {
                HelpText.Create("Press to teleport", FindAttachPosition("trackpad"), new Vector3(0f, 0.02f, 0.05f)),
                HelpText.Create("Circle to rotate", FindAttachPosition("trackpad"), new Vector3(0.05f, 0.02f, 0f), new Vector3(0.015f, 0f, 0f)),
                HelpText.Create("press & move controller", FindAttachPosition("trackpad"), new Vector3(-0.05f, 0.02f, 0f), new Vector3(-0.015f, 0f, 0f)),
                HelpText.Create("Warp into main char", FindAttachPosition("trigger"), new Vector3(0.06f, 0.04f, -0.05f)),
                HelpText.Create("reset area", FindAttachPosition("lgrip"), new Vector3(-0.06f, 0f, -0.05f))
            });
        }
    }
}
