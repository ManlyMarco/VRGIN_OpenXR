using UnityEngine;
using VRGIN.Core;

namespace Valve.VR
{
    public class SteamVR_Action_Vibration_Source : SteamVR_Action_Out_Source
    {
        protected SteamVR_Action_Vibration vibrationAction;

        public override bool active
        {
            get
            {
                if (activeBinding) return setActive;
                return false;
            }
        }

        public override bool activeBinding => true;

        public override bool lastActive { get; protected set; }

        public override bool lastActiveBinding => true;

        public float timeLastExecuted { get; protected set; }

        public event SteamVR_Action_Vibration.ActiveChangeHandler onActiveChange;

        public event SteamVR_Action_Vibration.ActiveChangeHandler onActiveBindingChange;

        public event SteamVR_Action_Vibration.ExecuteHandler onExecute;

        public override void Initialize()
        {
            base.Initialize();
            lastActive = true;
        }

        public override void Preinitialize(SteamVR_Action wrappingAction, SteamVR_Input_Sources forInputSource)
        {
            base.Preinitialize(wrappingAction, forInputSource);
            vibrationAction = (SteamVR_Action_Vibration)wrappingAction;
        }

        public void Execute(float secondsFromNow, float durationSeconds, float frequency, float amplitude)
        {
            if (!SteamVR_Input.isStartupFrame)
            {
                timeLastExecuted = Time.realtimeSinceStartup;
                var eVRInputError = OpenVR.Input.TriggerHapticVibrationAction(handle, secondsFromNow, durationSeconds, frequency, amplitude, inputSourceHandle);
                if (eVRInputError != 0) Debug.LogError("<b>[SteamVR]</b> TriggerHapticVibrationAction (" + fullPath + ") error: " + eVRInputError.ToString() + " handle: " + handle);
                if (onExecute != null)
                {
                    onExecute(vibrationAction, inputSource, secondsFromNow, durationSeconds, frequency, amplitude);
                }
            }
        }
    }
}
