using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRGIN.Core;
using VRGIN.Helpers;

namespace VRGIN.Controls.Handlers
{
    public class BodyRumbleHandler : ProtectedBehaviour
    {
        private Controller _Controller;

        private int _TouchCounter;

        private VelocityRumble _Rumble;

        protected override void OnStart()
        {
            base.OnStart();
            _Controller = GetComponent<Controller>();
            _Rumble = new VelocityRumble(_Controller.Tracking, 30, 10f, 3f, 1500, 10f);

            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if(mode == LoadSceneMode.Single && enabled)
            {
                try
                {
                    OnStop();
                }
                catch(Exception ex)
                {
                    VRLog.Error(ex);
                }
            }
        }

        protected void OnDisable()
        {
            OnStop();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            _Rumble.Device = _Controller.Tracking;
        }

        protected void OnTriggerEnter(Collider collider)
        {
            if (VR.Interpreter.IsBody(collider))
            {
                _TouchCounter++;
                _Controller.StartRumble(_Rumble);
                if (_TouchCounter == 1) _Controller.StartRumble(new RumbleImpulse(1000));
            }
        }

        protected void OnTriggerExit(Collider collider)
        {
            if (VR.Interpreter.IsBody(collider))
            {
                _TouchCounter--;
                if (_TouchCounter == 0) _Controller.StopRumble(_Rumble);
            }
        }

        protected void OnStop()
        {
            _TouchCounter = 0;
            if (_Controller) _Controller.StopRumble(_Rumble);
        }
    }
}
