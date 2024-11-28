using UnityEngine;
using UnityEngine.UI;
using VRGIN.Core;

namespace VRGIN.Controls
{
    public class HelpText : ProtectedBehaviour
    {
        private Vector3 _TextOffset;

        private Vector3 _LineOffset;

        private Vector3 _HeightVector;

        private Vector3 _MovementVector;

        private Transform _Target;

        private string _Text;

        private static Material S_Material;

        private LineRenderer _Line;

        public static HelpText Create(string text, Transform target, Vector3 textOffset, Vector3? lineOffset = null)
        {
            var helpText = new GameObject().AddComponent<HelpText>();
            helpText._Text = text;
            helpText._Target = target;
            helpText._TextOffset = textOffset;
            helpText._LineOffset = lineOffset.HasValue ? lineOffset.Value : Vector3.zero;
            var vector = lineOffset.HasValue ? textOffset - lineOffset.Value : textOffset;
            helpText._HeightVector = Vector3.Project(vector, Vector3.up);
            helpText._MovementVector = Vector3.ProjectOnPlane(vector, Vector3.up);
            return helpText;
        }

        protected override void OnStart()
        {
            base.OnStart();
            transform.SetParent(_Target, false);
            var canvas = new GameObject().AddComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
            canvas.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 70f);
            transform.rotation = _Target.parent.rotation;
            canvas.transform.localScale = new Vector3(0.0001549628f, 0.0001549627f, 0f);
            canvas.transform.localPosition = _TextOffset;
            canvas.transform.localRotation = Quaternion.Euler(90f, 180f, 180f);
            var text = new GameObject().AddComponent<Text>();
            text.transform.SetParent(canvas.transform, false);
            text.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            text.GetComponent<RectTransform>().anchorMax = Vector2.one;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = 40;
            text.resizeTextMinSize = 1;
            text.color = Color.black;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = _Text;
            _Line = gameObject.AddComponent<LineRenderer>();
            _Line.material = new Material(VR.Context.Materials.Sprite);
            _Line.SetColors(Color.cyan, Color.cyan);
            _Line.useWorldSpace = false;
            _Line.SetVertexCount(4);
            _Line.SetWidth(0.001f, 0.001f);
            Quaternion.Inverse(_Target.localRotation);
            _Line.SetPosition(0, _LineOffset + _HeightVector * 0.1f);
            _Line.SetPosition(1, _LineOffset + _HeightVector * 0.5f + _MovementVector * 0.2f);
            _Line.SetPosition(2, _TextOffset - _HeightVector * 0.5f - _MovementVector * 0.2f);
            _Line.SetPosition(3, _TextOffset - _HeightVector * 0.1f);
            var obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = _TextOffset - Vector3.up * 0.001f;
            obj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            obj.transform.localScale = new Vector3(0.05539737f, 0.009849964f, 0f);
            if (!S_Material)
            {
                S_Material = VRManager.Instance.Context.Materials.Unlit;
                S_Material.color = Color.white;
            }

            obj.transform.GetComponent<Renderer>().sharedMaterial = S_Material;
            obj.GetComponent<Collider>().enabled = false;
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
        }
    }
}
