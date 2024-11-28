using System.Diagnostics;
using System.Linq;

namespace VRGIN.Helpers
{
    public static class SteamVRDetector
    {
        public static bool IsRunning => Process.GetProcesses().Where(FilterInvalidProcesses).Any((Process process) => process.ProcessName == "vrcompositor");
        public static bool IsStudio => Process.GetProcesses().Where(FilterInvalidProcesses).Any((Process process) => process.ProcessName == "CharaStudio");

        private static bool FilterInvalidProcesses(Process p)
        {
            try
            {
                return p.ProcessName != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
