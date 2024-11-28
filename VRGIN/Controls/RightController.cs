using UnityEngine;

namespace VRGIN.Controls
{
    public class RightController : Controller
    {
        public static Controller Create()
        {
            if (VRGIN.Helpers.SteamVRDetector.IsStudio)
            {
                return new GameObject("Right Controller").AddComponent<StudioController>();
            }
            return new GameObject("Right Controller").AddComponent<RightController>();
        }
    }
}
    