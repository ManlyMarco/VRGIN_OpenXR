using UnityEngine;
using Valve.VR;
using VRGIN.Core;
using static SteamVR_Utils;

namespace VRGIN.Controls
{
    public class DeviceLegacyAdapter
    {
        private SteamVR_Behaviour_Pose _tracking;

        private static Nop_Action_Boolean NOP_ACTION_BOOLEAN = new Nop_Action_Boolean();

        private static Nop_Action_Vector2 NOP_ACTION_VECTOR2 = new Nop_Action_Vector2();

        public float hairTriggerDelta = 0.1f;

        private float hairTriggerLimit;

        private bool hairTriggerState;

        private bool hairTriggerPrevState;

        private int prevFrameCount = -1;

        private static EVRButtonId[] SUPPORTED_BUTTON_ID = new EVRButtonId[8]
        {
            EVRButtonId.k_EButton_System,
            EVRButtonId.k_EButton_ApplicationMenu,
            EVRButtonId.k_EButton_Grip,
            EVRButtonId.k_EButton_Axis0,
            EVRButtonId.k_EButton_Axis1,
            EVRButtonId.k_EButton_Axis2,
            EVRButtonId.k_EButton_Axis3,
            EVRButtonId.k_EButton_Axis4
        };

        private static ulong[] SUPPORTED_BUTTON_MASK = new ulong[8] { 1uL, 2uL, 4uL, 4294967296uL, 8589934592uL, 17179869184uL, 34359738368uL, 68719476736uL };

        public DeviceLegacyAdapter(SteamVR_Behaviour_Pose tracking)
        {
            _tracking = tracking;
        }

        public bool GetPress(EVRButtonId buttonId)
        {
            return GetActionBoolean_Press(buttonId).state;
        }

        public bool GetPressDown(EVRButtonId buttonId)
        {
            return GetActionBoolean_Press(buttonId).stateDown;
        }

        public bool GetPressUp(EVRButtonId buttonId)
        {
            return GetActionBoolean_Press(buttonId).stateUp;
        }

        public bool GetPress(ulong buttonMaxk)
        {
            for (var i = 0; i < SUPPORTED_BUTTON_ID.Length; i++)
            {
                var buttonId = SUPPORTED_BUTTON_ID[i];
                var num = SUPPORTED_BUTTON_MASK[i];
                if ((buttonMaxk & num) != 0L && GetPress(buttonId)) return true;
            }

            return false;
        }

        public bool GetPressDown(ulong buttonMaxk)
        {
            for (var i = 0; i < SUPPORTED_BUTTON_ID.Length; i++)
            {
                var buttonId = SUPPORTED_BUTTON_ID[i];
                var num = SUPPORTED_BUTTON_MASK[i];
                if ((buttonMaxk & num) != 0L && GetPressUp(buttonId)) return true;
            }

            return false;
        }

        public bool GetPressUp(ulong buttonMaxk)
        {
            var result = false;
            for (var i = 0; i < SUPPORTED_BUTTON_ID.Length; i++)
            {
                var buttonId = SUPPORTED_BUTTON_ID[i];
                var num = SUPPORTED_BUTTON_MASK[i];
                if ((buttonMaxk & num) != 0L)
                {
                    if (GetPress(buttonId)) return false;
                    if (GetPressUp(buttonId)) result = true;
                }
            }

            return result;
        }

        public bool GetTouch(EVRButtonId buttonId)
        {
            return GetActionBoolean_Touch(buttonId).state;
        }

        public bool GetTouchDown(EVRButtonId buttonId)
        {
            return GetActionBoolean_Touch(buttonId).stateDown;
        }

        public bool GetTouchUp(EVRButtonId buttonId)
        {
            return GetActionBoolean_Press(buttonId).stateUp;
        }

        public bool GetTouch(ulong buttonMaxk)
        {
            for (var i = 0; i < SUPPORTED_BUTTON_ID.Length; i++)
            {
                var buttonId = SUPPORTED_BUTTON_ID[i];
                var num = SUPPORTED_BUTTON_MASK[i];
                if ((buttonMaxk & num) != 0L && GetPress(buttonId)) return true;
            }

            return false;
        }

        public bool GetTouchDown(ulong buttonMaxk)
        {
            for (var i = 0; i < SUPPORTED_BUTTON_ID.Length; i++)
            {
                var buttonId = SUPPORTED_BUTTON_ID[i];
                var num = SUPPORTED_BUTTON_MASK[i];
                if ((buttonMaxk & num) != 0L && GetPressUp(buttonId)) return true;
            }

            return false;
        }

        public bool GetTouchUp(ulong buttonMaxk)
        {
            var result = false;
            for (var i = 0; i < SUPPORTED_BUTTON_ID.Length; i++)
            {
                var buttonId = SUPPORTED_BUTTON_ID[i];
                var num = SUPPORTED_BUTTON_MASK[i];
                if ((buttonMaxk & num) != 0L)
                {
                    if (GetTouch(buttonId)) return false;
                    if (GetTouchUp(buttonId)) result = true;
                }
            }

            return result;
        }

        public Vector2 GetAxis(EVRButtonId buttonId = EVRButtonId.k_EButton_Axis0)
        {
            return GetActionVector2(buttonId).axis;
        }

        public void TriggerHapticPulse(ushort durationMicroSec = 500, EVRButtonId buttonId = EVRButtonId.k_EButton_Axis0)
        {
            // What comes after is way beyond me, and it doesn't work. The simplest way works, enough for me.
            //SteamVR.instance.hmd.TriggerHapticPulse((uint)_tracking.inputSource, 0, durationMicroSec);
            OpenVR.System.TriggerHapticPulse((uint)_tracking.inputSource, 0, durationMicroSec);
            //_ = SteamVR_Actions.legacy_emulate;
            //var inputSource = _tracking.inputSource;
            //var durationSeconds = (float)((int)durationMicroSec / 1000) / 1000f;
            ////var durationSeconds = 0.5f;
            //var frequency = 100f;
            //var amplitude = 1f;
            //SteamVR_Actions.legacy_emulate.Huptic[inputSource].Execute(0f, durationSeconds, frequency, amplitude);
        }

        private void UpdateHairTrigger()
        {
            var inputSource = _tracking.inputSource;
            var steamVR_Action_Single_Source = SteamVR_Actions.legacy_emulate.Axis1_1D[inputSource];
            hairTriggerPrevState = hairTriggerState;
            var axis = steamVR_Action_Single_Source.axis;
            if (hairTriggerState)
            {
                if (axis < hairTriggerLimit - hairTriggerDelta || axis <= 0f) hairTriggerState = false;
            }
            else if (axis > hairTriggerLimit + hairTriggerDelta || axis >= 1f) hairTriggerState = true;

            hairTriggerLimit = hairTriggerState ? Mathf.Max(hairTriggerLimit, axis) : Mathf.Min(hairTriggerLimit, axis);
        }

        public void Update()
        {
            if (Time.frameCount != prevFrameCount)
            {
                prevFrameCount = Time.frameCount;
                UpdateHairTrigger();
            }
        }

        public bool GetHairTrigger()
        {
            Update();
            return hairTriggerState;
        }

        public bool GetHairTriggerDown()
        {
            Update();
            if (hairTriggerState) return !hairTriggerPrevState;
            return false;
        }

        public bool GetHairTriggerUp()
        {
            Update();
            if (!hairTriggerState) return hairTriggerPrevState;
            return false;
        }

        public ISteamVR_Action_Boolean GetActionBoolean_Press(EVRButtonId buttnId)
        {
            var legacy_emulate = SteamVR_Actions.legacy_emulate;
            var inputSource = _tracking.inputSource;
            return buttnId switch
            {
                EVRButtonId.k_EButton_A => legacy_emulate.A_Press[inputSource],
                EVRButtonId.k_EButton_ApplicationMenu => legacy_emulate.ApplicationMenu_Press[inputSource],
                EVRButtonId.k_EButton_Axis0 => legacy_emulate.Axis0_Press[inputSource],
                EVRButtonId.k_EButton_Axis1 => legacy_emulate.Axis1_Press[inputSource],
                EVRButtonId.k_EButton_Axis2 => legacy_emulate.Axis2_Press[inputSource],
                EVRButtonId.k_EButton_Axis3 => legacy_emulate.Axis3_Press[inputSource],
                EVRButtonId.k_EButton_Axis4 => legacy_emulate.Axis4_Press[inputSource],
                EVRButtonId.k_EButton_Grip => legacy_emulate.Grip_Press[inputSource],
                EVRButtonId.k_EButton_System => legacy_emulate.System_Press[inputSource],
                _ => NOP_ACTION_BOOLEAN
            };
        }

        public ISteamVR_Action_Boolean GetActionBoolean_Touch(EVRButtonId buttnId)
        {
            var legacy_emulate = SteamVR_Actions.legacy_emulate;
            var inputSource = _tracking.inputSource;
            return buttnId switch
            {
                EVRButtonId.k_EButton_A => legacy_emulate.A_Touch[inputSource],
                EVRButtonId.k_EButton_ApplicationMenu => legacy_emulate.ApplicationMenu_Touch[inputSource],
                EVRButtonId.k_EButton_Axis0 => legacy_emulate.Axis0_Touch[inputSource],
                EVRButtonId.k_EButton_Axis1 => legacy_emulate.Axis1_Touch[inputSource],
                EVRButtonId.k_EButton_Axis2 => legacy_emulate.Axis2_Touch[inputSource],
                EVRButtonId.k_EButton_Axis3 => legacy_emulate.Axis3_Touch[inputSource],
                EVRButtonId.k_EButton_Axis4 => legacy_emulate.Axis4_Touch[inputSource],
                EVRButtonId.k_EButton_Grip => legacy_emulate.Grip_Touch[inputSource],
                EVRButtonId.k_EButton_System => legacy_emulate.System_Touch[inputSource],
                _ => NOP_ACTION_BOOLEAN
            };
        }

        public ISteamVR_Action_Vector2 GetActionVector2(EVRButtonId buttnId)
        {
            var legacy_emulate = SteamVR_Actions.legacy_emulate;
            var inputSource = _tracking.inputSource;
            return buttnId switch
            {
                EVRButtonId.k_EButton_Axis0 => legacy_emulate.Axis0_2D[inputSource],
                EVRButtonId.k_EButton_Axis1 => legacy_emulate.Axis1_2D[inputSource],
                EVRButtonId.k_EButton_Axis2 => legacy_emulate.Axis2_2D[inputSource],
                EVRButtonId.k_EButton_Axis3 => legacy_emulate.Axis3_2D[inputSource],
                EVRButtonId.k_EButton_Axis4 => legacy_emulate.Axis4_2D[inputSource],
                EVRButtonId.k_EButton_Grip => legacy_emulate.Grip_2D[inputSource],
                _ => NOP_ACTION_VECTOR2
            };
        }
    }
}
