using UnityEngine;
using Valve.VR;
using VRGIN.Controls.Handlers;
using VRGIN.Core;

namespace VRGIN.Controls
{
    public class LeftController : Controller
    {
        public static Controller Create()
        {
            if (VRGIN.Helpers.SteamVRDetector.IsStudio)
            {
                return new GameObject("Left Controller").AddComponent<StudioController>();
            }
            return new GameObject("Left Controller").AddComponent<LeftController>();
        }
    }
}
