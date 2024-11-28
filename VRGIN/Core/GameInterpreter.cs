using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VRGIN.Core
{
    /// <summary>
    /// Class that is responsible to collect all required data from the game 
    /// that is created or managed at runtime.
    /// </summary>
    public class GameInterpreter : ProtectedBehaviour
    {
        /// <summary>
        /// Gets a list of actors in the game. Used frequently.
        /// </summary>
        public virtual IEnumerable<IActor> Actors
        {
            get { yield break; }
        }

        public virtual bool IsEveryoneHeaded => Actors.All((IActor a) => a.HasHead);

        public virtual int DefaultCullingMask => LayerMask.GetMask("Default", "Chara");

        /// <summary>
        /// Finds the first actor who has no head (= is impersonated) or NULL.
        /// </summary>
        public virtual IActor FindImpersonatedActor()
        {
            return Actors.FirstOrDefault((IActor a) => !a.HasHead);
        }

        public virtual IActor FindNextActorToImpersonate()
        {
            var list = Actors.ToList();
            var actor2 = FindImpersonatedActor();
            if (actor2 != null) list.Remove(actor2);
            return list.OrderByDescending((IActor actor) => Vector3.Dot((actor.Eyes.position - VR.Camera.transform.position).normalized, VR.Camera.SteamCam.head.forward)).FirstOrDefault();
        }

        /// <summary>
        /// Finds the main camera object.
        /// </summary>
        public virtual Camera FindCamera()
        {
            return Camera.main;
        }

        /// <summary>
        /// Finds additional cameras that should be considered (i.e. added to the culling mask).
        /// </summary>
        public virtual IEnumerable<Camera> FindSubCameras()
        {
            return Camera.allCameras.Where((Camera c) => c.targetTexture == null).Except(new Camera[1] { Camera.main });
        }

        public CameraJudgement JudgeCamera(Camera camera)
        {
            if (camera.name.Contains("VRGIN") || camera.name == "poseUpdater") return CameraJudgement.Ignore;
            return JudgeCameraInternal(camera);
        }

        protected virtual CameraJudgement JudgeCameraInternal(Camera camera)
        {
            var flag = VR.GUI.IsInterested(camera);
            if (camera.targetTexture == null)
            {
                if (flag) return CameraJudgement.GUIAndCamera;
                if (camera.CompareTag("MainCamera")) return CameraJudgement.MainCamera;
                return CameraJudgement.SubCamera;
            }

            if (!flag) return CameraJudgement.Ignore;
            return CameraJudgement.GUI;
        }

        /// <summary>
        /// Checks whether the collider is to be interpreted as body part.
        /// </summary>
        public virtual bool IsBody(Collider collider)
        {
            return false;
        }

        /// <summary>
        /// Checks if a given canvas should be ignored.
        /// </summary>
        public virtual bool IsIgnoredCanvas(Canvas canvas)
        {
            return false;
        }

        /// <summary>
        /// Checks whether an effect is eligible for VR. 
        /// </summary>
        public virtual bool IsAllowedEffect(MonoBehaviour effect)
        {
            return !VR.Settings.EffectBlacklist.Contains(effect.GetType().Name);
        }

        /// <summary>
        /// Whether or not it's save to disable this camera.
        /// </summary>
        public virtual bool IsIrrelevantCamera(Camera blueprint)
        {
            return true;
        }

        /// <summary>
        /// Determines whether or not a camera is used to render the GUI. If you return true, this camera will render on top of the VRGUI camera.
        /// Note that this is mainly used to deal with NGUI, but it can also come in handy iff there is a world-space uGUI canvas that is used like a screen-space one. Usually,
        /// you should not need to tinker with this method.
        /// </summary>
        public virtual bool IsUICamera(Camera camera)
        {
            return camera.GetComponent("UICamera");
        }
    }
}
