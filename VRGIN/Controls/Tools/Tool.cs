using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;

namespace VRGIN.Controls.Tools
{
    public abstract class Tool : ProtectedBehaviour
    {

        protected SteamVR_Behaviour_Pose Tracking;
        private DeviceLegacyAdapter _Controller;
        protected Controller Owner;
        protected Controller Neighbor;


        public abstract Texture2D Image { get; }

        public GameObject Icon { get; set; }

        protected bool IsTracking
        {
            get
            {
                if ((bool)Tracking) return Tracking.isValid;
                return false;
            }
        }

        protected DeviceLegacyAdapter Controller => _Controller;

        protected Controller OtherController => Neighbor;

        protected virtual void OnDestroy()
        {

        }
        protected override void OnAwake()
        {
            base.OnAwake();
            Tracking = GetComponent<SteamVR_Behaviour_Pose>();
            _Controller = new DeviceLegacyAdapter(Tracking);
            Owner = GetComponentInChildren<Controller>();
        }

        protected override void OnStart()
        {
            base.OnStart();
            Neighbor = VR.Mode.Left == Owner ? VR.Mode.Right : VR.Mode.Left;
            VRLog.Info(Neighbor ? "Got my neighbor!" : "No neighbor");
        }

        protected virtual void OnEnable()
        {
            //VRLog.Info("On Enable: {0}", GetType().Name);
            if ((bool)Icon)
                Icon.SetActive(true);
            //else
            //    VRLog.Info("But no icon...");
        }

        protected virtual void OnDisable()
        {
            //VRLog.Info("On Disable: {0}", GetType().Name);
            if ((bool)Icon)
                Icon.SetActive(false);
            //else
                //VRLog.Info("But no icon...");
        }

        public virtual List<HelpText> GetHelpTexts()
        {
            return new List<HelpText>();
        }

        protected Transform FindAttachPosition(params string[] names)
        {
            return Owner.FindAttachPosition(names);
        }
    }
}
