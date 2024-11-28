using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using VRGIN.Controls.Handlers;
using VRGIN.Controls.Tools;
using VRGIN.Core;
using VRGIN.Helpers;

namespace VRGIN.Controls
{
    public abstract class Controller : ProtectedBehaviour
    {
        public class Lock
        {
            private Controller _Controller;

            public static readonly Lock Invalid = new Lock();

            public bool IsInvalidating { get; private set; }

            public bool IsValid { get; private set; }

            public bool KeepsTool { get; private set; }


            private Lock()
            {
                IsValid = false;
            }

            internal Lock(Controller controller)
            {
                IsValid = true;
                _Controller = controller;
                _Controller._Lock = this;
                _Controller.OnLock();
            }

            public void Release()
            {
                if (IsValid)
                {
                    _Controller._Lock = null;
                    _Controller.OnUnlock();
                    IsValid = false;
                }
                else
                    VRLog.Warn("Tried to release an invalid lock!");
            }

            public void SafeRelease()
            {
                if (IsValid)
                    IsInvalidating = true;
                else
                    VRLog.Warn("Tried to release an invalid lock!");
            }
        }

        //private bool _Started;


        public SteamVR_Behaviour_Pose Tracking;

        protected DeviceLegacyAdapter _Input;

        //protected BoxCollider Collider;

        //private float? appButtonPressTime;

        public List<Tool> Tools = new List<Tool>();

        public Controller Other;

       // private const float APP_BUTTON_TIME_THRESHOLD = 0.5f;

        //private bool helpShown;

        //private List<HelpText> helpTexts;

        //private Canvas _Canvas;

        protected Lock _Lock = Lock.Invalid;

        //private GameObject _AlphaConcealer;

        public SteamVR_RenderModel Model { get; protected set; }

        public RumbleManager Rumble { get; protected set; }

        public virtual int ToolIndex { get; set; }

        public Tool ActiveTool
        {
            get
            {
                if (ToolIndex >= Tools.Count) return null;
                return Tools[ToolIndex];
            }
        }

        public virtual IList<Type> ToolTypes => new List<Type>();

        public virtual bool ToolEnabled
        {
            get
            {
                return ActiveTool != null && ActiveTool.enabled;
            }
            set
            {
                if (ActiveTool != null)
                {
                    ActiveTool.enabled = value;
                }
            }
        }

        public bool IsTracking
        {
            get
            {
                return Tracking && Tracking.isValid;
            }
        }

        public DeviceLegacyAdapter Input => _Input;

        public SteamVR_Input_Sources InputSources
        {
            get
            {
                if ((bool)Tracking)
                {
                    return Tracking.inputSource;
                }
                throw new NullReferenceException("Tracking is null");
            }
            set
            {
                if ((bool)Tracking) Tracking.inputSource = value;
            }
        }

        [Obsolete("Use TryAcquireFocus() or AcquireFocus()")]
        public bool AcquireFocus(out Lock lockObj)
        {
            return TryAcquireFocus(out lockObj);
        }

        public bool TryAcquireFocus(out Lock lockObj)
        {
            lockObj = null;
            if (CanAcquireFocus())
            {
                lockObj = new Lock(this);
                return true;
            }
            return false;
        }

        public Lock AcquireFocus()
        {
            if (TryAcquireFocus(out var lockObj))
            {
                return lockObj;
            }
            return Lock.Invalid;
        }

        public bool CanAcquireFocus()
        {
            if (_Lock != null)
            {
                return !_Lock.IsValid;
            }
            return true;
        }

        protected virtual void OnLock()
        {
            ToolEnabled = false;
                //_AlphaConcealer.SetActive(false);
        }

        protected virtual void OnUnlock()
        {
            ToolEnabled = true;
            //_AlphaConcealer.SetActive(true);
        }

        protected virtual void OnDestroy()
        {
            VRLog.Info(" Controller OnDestroy called. " + gameObject.name);
            SteamVR_Events.RenderModelLoaded.Remove(_OnRenderModelLoaded);
        }

        protected virtual void SetUp()
        {
            SteamVR_Events.RenderModelLoaded.Listen(_OnRenderModelLoaded);

            Tracking = gameObject.AddComponent<SteamVR_Behaviour_Pose>();
            _Input = new DeviceLegacyAdapter(Tracking);

            Rumble = gameObject.AddComponent<RumbleManager>();
            gameObject.AddComponent<BodyRumbleHandler>();
            gameObject.AddComponent<MenuHandler>();

            Model = new GameObject("Model").AddComponent<SteamVR_RenderModel>();
            Model.shader = VRManager.Instance.Context.Materials.StandardShader;
            if (!Model.shader) VRLog.Warn("Shader not found");

            Model.transform.SetParent(transform, false);
            Model.transform.localPosition = Vector3.zero;
            Model.transform.localRotation = Quaternion.identity;
            Model.gameObject.layer = 0;
        }

        protected void _OnRenderModelLoaded(SteamVR_RenderModel model, bool isLoaded)
        {
            try
            {
                if ((bool)model && model.transform.IsChildOf(transform))
                {
                    VRLog.Info("Render model loaded! rendeModelName: '" + model.renderModelName + "'");
                    gameObject.SendMessageToAll("OnRenderModelLoaded");
                    //OnRenderModelLoaded();
                }
            }
            catch (Exception obj)
            {
                VRLog.Error(obj);
            }
        }

        //private void OnRenderModelLoaded()
        //{
        //    //var componentsInChildren = Model.GetComponentsInChildren<Renderer>(true);
        //    //foreach (var renderer in componentsInChildren)
        //    //    VRLog.Debug($"Name: {renderer.gameObject.name}, Layer: {LayerMask.LayerToName(renderer.gameObject.layer)}, Visible: {renderer.isVisible}, Shader: {renderer.sharedMaterial.shader}");
        //}

        protected override void OnAwake()
        {
            base.OnAwake();
            SetUp();
        }

        public virtual void AddTool(Type toolType)
        {
            if (toolType.IsSubclassOf(typeof(Tool)) && !Tools.Any((Tool tool) => toolType.IsAssignableFrom(tool.GetType())))
            {
                var tool2 = gameObject.AddComponent(toolType) as Tool;
                Tools.Add(tool2);
                //CreateToolCanvas(tool2);

                tool2.enabled = false;
            }
        }

        public void AddTool<T>() where T : Tool
        {
            AddTool(typeof(T));
        }

        protected override void OnStart()
        {
            var num = 0;
            foreach (var tool in Tools)
            {
                if (num++ != ToolIndex && (bool)tool)
                {
                    tool.enabled = false;
                    VRLog.Info("Disable tool #{0} ({1})", num - 1, ToolIndex);
                    continue;
                }

                VRLog.Info("Enable Tool #{0}", num - 1);
                if (tool.enabled) tool.enabled = false;
                tool.enabled = true;
            }
        }
        protected override void OnUpdate()
        {
            // An option to disable controller ? pretty sure it doesn't stop or maybe takes awhile.
            //if (!Tracking) return;
            //_ = InputSources;
            if (_Lock != null && _Lock.IsInvalidating)
            {
                TryReleaseLock();
            }
            //if (_Lock != null && _Lock.IsValid) return;
            //if (Input.GetPressDown(EVRButtonId.k_EButton_ApplicationMenu)) appButtonPressTime = Time.unscaledTime;
            //if (Input.GetPress(EVRButtonId.k_EButton_ApplicationMenu) && Time.unscaledTime - appButtonPressTime > 0.5f)
            //{
            //    //ShowHelp();
            //    appButtonPressTime = null;
            //}
            //if (!Input.GetPressUp(EVRButtonId.k_EButton_ApplicationMenu)) return;
            //if (helpShown)
            //{
            //    Model.gameObject.SetActive(true);
            //   //HideHelp();
            //}
            //else
            //{
            //    if ((bool)ActiveTool) ActiveTool.enabled = false;
            //    ToolIndex = (ToolIndex + 1) % Tools.Count;
            //    if ((bool)ActiveTool) ActiveTool.enabled = true;
            //}

            //appButtonPressTime = null;
        }

        protected void TryReleaseLock()
        {
            var input = Input;
            foreach (var item in Enum.GetValues(typeof(EVRButtonId)).OfType<EVRButtonId>())
            {
                if (input.GetPress(item)) return;
            }

            _Lock.Release();
        }

        public void StartRumble(IRumbleSession session)
        {
            Rumble.StartRumble(session);
        }

        public void StopRumble(IRumbleSession session)
        {
            Rumble.StopRumble(session);
        }

        //private void HideHelp()
        //{
        //    if (helpShown)
        //    {
        //        helpTexts.ForEach(delegate(HelpText h) { Destroy(h.gameObject); });
        //        helpShown = false;
        //    }
        //}

        //private void ShowHelp()
        //{
        //    if (ActiveTool != null)
        //    {
        //        helpTexts = ActiveTool.GetHelpTexts();
        //        helpShown = true;
        //    }
        //}

        //private void BuildCanvas()
        //{
        //    var canvas = _Canvas = new GameObject("ToolIconCanvas").AddComponent<Canvas>();
        //    canvas.renderMode = RenderMode.WorldSpace;
        //    canvas.transform.SetParent(transform, false);

        //    canvas.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 950f);
        //    canvas.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 950f);


        //    // Pico 4 setup.
        //    canvas.transform.localPosition = new Vector3(0f, 0f, -0.025f);//(0f, -0.02f, -0.02f);//Vector3(0, -0.02725995f, 0.0279f);
        //    canvas.transform.localRotation = Quaternion.Euler(120f, 0, 0); ;//Quaternion.Euler(30, 180, 180);
        //    canvas.transform.localScale = new Vector3(0.00002f, 0.00002f, 0);  //(4.930151e-05f, 4.930148e-05f, 0);

        //    // Original
        //    //canvas.transform.localPosition = new Vector3(0f, -0.02725995f, 0.0279f);
        //    //canvas.transform.localRotation = Quaternion.Euler(30f, 180f, 180f);
        //    //canvas.transform.localScale = new Vector3(4.930151E-05f, 4.930148E-05f, 0f);
        //    //_AlphaConcealer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //    //_AlphaConcealer.transform.SetParent(transform, false);
        //    //_AlphaConcealer.transform.localScale = new Vector3(0.05f, 0.0001f, 0.05f);
        //    //_AlphaConcealer.transform.localPosition = new Vector3(0, -0.0303f, 0.0142f);
        //    //_AlphaConcealer.transform.localRotation = Quaternion.Euler(60, 0, 0);
        //    //_AlphaConcealer.GetComponent<Collider>().enabled = false;

        //    canvas.gameObject.layer = 0;
        //}

        //private void CreateToolCanvas(Tool tool)
        //{
        //    var image = new GameObject("ToolCanvas").AddComponent<Image>();
        //    image.transform.SetParent(_Canvas.transform, false);
        //    var image2 = tool.Image;
        //    image.sprite = Sprite.Create(image2, new Rect(0f, 0f, image2.width, image2.height), new Vector2(0.5f, 0.5f));
        //    image.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
        //    image.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
        //    image.color = Color.cyan;
        //    tool.Icon = image.gameObject;
        //    tool.Icon.SetActive(false);
        //    tool.Icon.layer = 0;
        //}

        public Transform FindAttachPosition(params string[] names)
        {
            var transform = (from t in this.transform.GetComponentsInChildren<Transform>(true)
                             where names.Contains(t.name)
                             select t).FirstOrDefault();
            if (transform == null) return null;
            return transform.Find("attach");
        }

        public enum TrackpadDirection
        {
            Up,
            Down,
            Left,
            Right,
            Center,
        }

        public TrackpadDirection GetTrackpadDirection()
        {
            var touchPosition = Input.GetAxis();
            var threshold = VR.Settings.TouchpadThreshold;
            if (touchPosition.sqrMagnitude < threshold * threshold)
            {
                return TrackpadDirection.Center;
            }
            else if(Mathf.Abs(touchPosition.y) < touchPosition.x)
            {
                return TrackpadDirection.Right;
            }
            else if(Mathf.Abs(touchPosition.x) < touchPosition.y)
            {
                return TrackpadDirection.Up;
            }
            else if(touchPosition.x < -Mathf.Abs(touchPosition.y))
            {
                return TrackpadDirection.Left;
            }
            else
            {
                return TrackpadDirection.Down;
            }
        }
    }
}
