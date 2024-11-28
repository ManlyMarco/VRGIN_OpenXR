using VRGIN.Modes;
using WindowsInput;

namespace VRGIN.Core
{
    /// <summary>
    /// Helper class that gives you easy access to all crucial objects.
    /// </summary>
    public static class VR
    {
        public static GameInterpreter Interpreter => VRManager.Instance.Interpreter;
        public static VRCamera Camera => VRCamera.Instance;
        public static VRGUI GUI => VRGUI.Instance;
        public static IVRManagerContext Context => VRManager.Instance.Context;
        public static ControlMode Mode => VRManager.Instance.Mode;
        public static VRSettings Settings => Context.Settings;
        public static Shortcuts Shortcuts => Context.Settings.Shortcuts;
        public static VRManager Manager => VRManager.Instance;

        public static InputSimulator Input => VRManager.Instance.Input;

        public static HMDType HMD => VRManager.Instance.HMD;
        public static bool Active { get; set; }
        public static bool Quitting { get; internal set; }
    }
}
