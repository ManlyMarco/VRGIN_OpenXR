using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRGIN.Core;
using VRGIN.Helpers;

namespace VRGIN.Controls
{
    public class RumbleManager : ProtectedBehaviour
    {
        //private const float MILLI_TO_SECONDS = 0.001f;
        //public const float MIN_INTERVAL = 0.0050000004f;

        private HashSet<IRumbleSession> _RumbleSessions = new HashSet<IRumbleSession>();

        private float _LastImpulse;

        private Controller _Controller;

        protected override void OnStart()
        {
            base.OnStart();
            _Controller = GetComponent<Controller>();
        }

        protected virtual void OnDisable()
        {
            _RumbleSessions.Clear();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (_RumbleSessions.Count > 0)
            {
                var session = _RumbleSessions.Max();
                var timeSinceLastImpulse = Time.unscaledTime - _LastImpulse;

                if (_Controller.Tracking.isValid && timeSinceLastImpulse >= session.MilliInterval * 0.001f && timeSinceLastImpulse > 0.005f)
                {
                    if (session.IsOver)
                    {
                        _RumbleSessions.Remove(session);
                    }
                    else
                    {
                        if (VR.Settings.Rumble)
                        {
                            _Controller.Input.TriggerHapticPulse(session.MicroDuration);
                        }
                        _LastImpulse = Time.unscaledTime;
                        session.Consume();
                    }

                }
            }
        }

        public void StartRumble(IRumbleSession session)
        {
            _RumbleSessions.Add(session);
        }

        internal void StopRumble(IRumbleSession session)
        {
            _RumbleSessions.Remove(session);
        }
    }
}
