using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using VRGIN.Controls.Handlers;
using VRGIN.Controls.Tools;
using VRGIN.Core;

namespace VRGIN.Controls
{
    internal class StudioController : Controller
    {
        protected BoxCollider Collider;

        private float? appButtonPressTime;

        private const float APP_BUTTON_TIME_THRESHOLD = 0.5f;

        private bool helpShown;

        private List<HelpText> helpTexts;

        private Canvas _Canvas;

        private GameObject _AlphaConcealer;

        public override bool ToolEnabled
        {
            get
            {
                if (ActiveTool != null) return ActiveTool.enabled;
                return false;
            }
            set
            {
                if (ActiveTool != null)
                {
                    ActiveTool.enabled = value;
                    if (!value) HideHelp();
                }
            }
        }

        protected override void OnLock()
        {
            ToolEnabled = false;
            _AlphaConcealer.SetActive(false);
        }

        protected override void OnUnlock()
        {
            ToolEnabled = true;
            _AlphaConcealer.SetActive(true);
        }

        protected override void SetUp()
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
            BuildCanvas();
            Collider = new GameObject("Collider").AddComponent<BoxCollider>();
            Collider.transform.SetParent(transform, false);
            Collider.center = new Vector3(0f, -0.02f, -0.06f);
            Collider.size = new Vector3(0.05f, 0.05f, 0.2f);
            Collider.isTrigger = true;
            gameObject.AddComponent<Rigidbody>().isKinematic = true;
        }
        public override void AddTool(Type toolType)
        {
            if (toolType.IsSubclassOf(typeof(Tool)) && !Tools.Any((Tool tool) => toolType.IsAssignableFrom(tool.GetType())))
            {
                var tool2 = gameObject.AddComponent(toolType) as Tool;
                Tools.Add(tool2);
                CreateToolCanvas(tool2);
                tool2.enabled = false;
            }
        }

        protected override void OnUpdate()
        {
            if (!Tracking) return;
            _ = InputSources;
            if (_Lock != null && _Lock.IsInvalidating) TryReleaseLock();
            if (_Lock != null && _Lock.IsValid) return;
            if (Input.GetPressDown(EVRButtonId.k_EButton_ApplicationMenu)) appButtonPressTime = Time.unscaledTime;
            if (Input.GetPress(EVRButtonId.k_EButton_ApplicationMenu) && Time.unscaledTime - appButtonPressTime > 0.5f)
            {
                ShowHelp();
                appButtonPressTime = null;
            }

            if (!Input.GetPressUp(EVRButtonId.k_EButton_ApplicationMenu)) return;
            if (helpShown)
                HideHelp();
            else
            {
                if ((bool)ActiveTool) ActiveTool.enabled = false;
                ToolIndex = (ToolIndex + 1) % Tools.Count;
                if ((bool)ActiveTool) ActiveTool.enabled = true;
            }

            appButtonPressTime = null;
        }

        private void HideHelp()
        {
            if (helpShown)
            {
                helpTexts.ForEach(delegate (HelpText h) { Destroy(h.gameObject); });
                helpShown = false;
            }
        }

        private void ShowHelp()
        {
            if (ActiveTool != null)
            {
                helpTexts = ActiveTool.GetHelpTexts();
                helpShown = true;
            }
        }

        private void BuildCanvas()
        {
            var canvas = _Canvas = new GameObject("ToolIconCanvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 950f);
            canvas.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 950f);
            canvas.transform.localPosition = new Vector3(0f, -0.02725995f, 0.0279f);
            canvas.transform.localRotation = Quaternion.Euler(30f, 180f, 180f);
            canvas.transform.localScale = new Vector3(4.930151E-05f, 4.930148E-05f, 0f);
            canvas.gameObject.layer = 0;
            _AlphaConcealer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _AlphaConcealer.transform.SetParent(transform, false);
            _AlphaConcealer.transform.localScale = new Vector3(0.05f, 0.0001f, 0.05f);
            _AlphaConcealer.transform.localPosition = new Vector3(0, -0.0303f, 0.0142f);
            _AlphaConcealer.transform.localRotation = Quaternion.Euler(60, 0, 0);
            _AlphaConcealer.GetComponent<Collider>().enabled = false;
        }
        private void CreateToolCanvas(Tool tool)
        {
            var image = new GameObject("ToolCanvas").AddComponent<Image>();
            image.transform.SetParent(_Canvas.transform, false);
            var image2 = tool.Image;
            image.sprite = Sprite.Create(image2, new Rect(0f, 0f, image2.width, image2.height), new Vector2(0.5f, 0.5f));
            image.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            image.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            image.color = Color.cyan;
            tool.Icon = image.gameObject;
            tool.Icon.SetActive(false);
            tool.Icon.layer = 0;
        }
    }
}
