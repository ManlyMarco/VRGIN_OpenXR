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

        private Vector3 _PrevPoint;
        private float? _TriggerDownTime;
        private bool Showing;

        private List<Vector2> _Points = new List<Vector2>();

        private const float EXACT_IMPERSONATION_TIME = 1f;
        private Controller.Lock _SelfLock = Controls.Controller.Lock.Invalid;
        private float _IPDOnStart;

        private GrabAction _Grab;
        private TravelDistanceRumble _TravelRumble;


        //private float? _GripStartTime;




        //private const float GRIP_TIME_THRESHOLD = 0.1f;

        //private const float GRIP_DIFF_THRESHOLD = 0.01f;


        //private Vector3 _PrevControllerPos;

        //private Quaternion _PrevControllerRot;

        //private Controller.Lock _OtherLock;

        //private float _InitialControllerDistance;

        // private float _InitialIPD;

        //private Vector3 _PrevFromTo;

        //private const EVRButtonId SECONDARY_SCALE_BUTTON = EVRButtonId.k_EButton_Axis1;

        //private const EVRButtonId SECONDARY_ROTATE_BUTTON = EVRButtonId.k_EButton_Grip;


        //private bool _ScaleInitialized;

        //private bool _RotationInitialized;

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

        private void OnDestroy()
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
                var v = Controller.GetAxis(EVRButtonId.k_EButton_Axis0);
                _ProspectedPlayArea.Reset();
                if (v.x < -0.2f)
                {
                    _ProspectedPlayArea.Rotation -= 20f;
                }
                else if (v.x > 0.2f)
                {
                    _ProspectedPlayArea.Rotation += 20f;
                }
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
                    if (Controller.GetTouchDown(EVRButtonId.k_EButton_Axis0))
                    {
                        EnterState(WarpState.Rotating);
                    }
                }
                else
                {
                    CheckRotationalPress();
                }

                if (Owner.Input.GetPressDown(EVRButtonId.k_EButton_Grip))
                {
                    EnterState(WarpState.Grabbing);
                }
            }

            if (State == WarpState.Grabbing)
            {
                switch (_Grab.HandleGrabbing())
                {
                    case GrabAction.Status.Continue:
                        break;
                    case GrabAction.Status.DoneQuick:
                        EnterState(WarpState.None);
                        Owner.StartRumble(new RumbleImpulse(800));
                        _ProspectedPlayArea.Height = 0;
                        _ProspectedPlayArea.Scale = _IPDOnStart;
                        break;
                    case GrabAction.Status.DoneSlow:
                        EnterState(WarpState.None);
                        ResetPlayArea(_ProspectedPlayArea);
                        break;
                }
            }
            if (State == WarpState.Rotating)
            {
                HandleRotation();
            }
            if (State == WarpState.Transforming && Controller.GetPressUp(EVRButtonId.k_EButton_Axis0))
            {
                _ProspectedPlayArea.Apply();

                ArcRenderer.Update();

                EnterState(WarpState.Rotating);
            }

            if (State == WarpState.None)
            {
                if (Controller.GetHairTriggerDown())
                {
                    _TriggerDownTime = Time.unscaledTime;
                }
                if (_TriggerDownTime != null)
                {
                    if (Controller.GetHairTrigger() && (Time.unscaledTime - _TriggerDownTime) > EXACT_IMPERSONATION_TIME)
                    {
                        VRManager.Instance.Mode.Impersonate(VR.Interpreter.FindNextActorToImpersonate(),
                            ImpersonationMode.Exactly);
                        _TriggerDownTime = null;
                    }
                    if (VRManager.Instance.Interpreter.Actors.Any() && Controller.GetHairTriggerUp())
                    {
                        VRManager.Instance.Mode.Impersonate(VR.Interpreter.FindNextActorToImpersonate(),
                            ImpersonationMode.Approximately);
                    }
                }
            }
        }

        private void HandleRotation()
        {
            if (Showing)
            {
                _Points.Add(Controller.GetAxis(EVRButtonId.k_EButton_Axis0));

                if (_Points.Count > 2)
                {
                    DetectCircle();
                }
            }

            if (Controller.GetPressDown(EVRButtonId.k_EButton_Axis0))
            {
                EnterState(WarpState.Transforming);
            }

            if (Controller.GetTouchUp(EVRButtonId.k_EButton_Axis0))
            {
                EnterState(WarpState.None);
            }
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
            VRLog.Debug($"EnterState {state}");
            switch (State)
            {
                case WarpState.None:
                    _SelfLock = Owner.AcquireFocus(keepTool: true);
                    break;
                case WarpState.Rotating:

                    break;
                case WarpState.Grabbing:
                    _Grab.Destroy();
                    _Grab = null;
                    break;
            }

            // ENTER state
            switch (state)
            {
                case WarpState.None:
                    SetVisibility(false);
                    if (_SelfLock.IsValid)
                    {
                        _SelfLock.Release();
                    }
                    break;
                case WarpState.Rotating:
                    SetVisibility(true);
                    Reset();
                    break;
                case WarpState.Grabbing:
                    _Grab = new GrabAction(Owner, EVRButtonId.k_EButton_Grip);
                    break;
            }

            State = state;
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
