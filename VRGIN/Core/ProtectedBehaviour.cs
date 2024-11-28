using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRGIN.Core
{
    public class ProtectedBehaviour : MonoBehaviour
    {
        //private static IDictionary<string, double> PerformanceTable = new Dictionary<string, double>();

        //private string GetKey(string method)
        //{
        //    return $"{GetType().FullName}#{method}";
        //}

        protected void Start()
        {
            // Our ride is pretty much safe, no need for extra memory eaters.
            //SafelyCall(OnStart);
            OnStart();
        }

        protected void Awake()
        {
            //SafelyCall(OnAwake);
            OnAwake();
        }

        protected void Update()
        {
            //SafelyCall(OnUpdate);
            OnUpdate();
        }

        protected void LateUpdate()
        {
            //SafelyCall(OnLateUpdate);
            OnLateUpdate();
        }

        protected void FixedUpdate()
        {
            //SafelyCall(OnFixedUpdate);
            OnFixedUpdate();
        }

        protected virtual void OnStart() { }

        protected virtual void OnUpdate() { }

        protected virtual void OnLateUpdate() { }

        protected virtual void OnFixedUpdate() { }

        protected virtual void OnAwake() { }

        //private void SafelyCall(Action action)
        //{
        //    try
        //    {
        //        action();
        //    }
        //    catch (Exception obj)
        //    {
        //        VRLog.Error(obj);
        //    }
        //}

        //public static void DumpTable()
        //{
        //    VRLog.Info("DUMP");
        //    var stringBuilder = new StringBuilder();
        //    var enumerator = PerformanceTable.GetEnumerator();
        //    while (enumerator.MoveNext()) stringBuilder.AppendFormat("{1}ms: {0}\n", enumerator.Current.Key, enumerator.Current.Value / (double)Time.realtimeSinceStartup);
        //    File.WriteAllText("performance.txt", stringBuilder.ToString());
        //}

        //public void Invoke(Action action, float delayInSeconds)
        //{
        //    StartCoroutine(_Invoke(action, delayInSeconds));
        //}

        //private IEnumerator _Invoke(Action action, float delay)
        //{
        //    yield return new WaitForSeconds(delay);
        //    try
        //    {
        //        action();
        //    }
        //    catch (Exception obj)
        //    {
        //        VRLog.Error(obj);
        //    }
        //}
    }
}
