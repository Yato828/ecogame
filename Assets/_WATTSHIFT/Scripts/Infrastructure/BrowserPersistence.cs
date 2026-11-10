using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Wattshift
{
    /// <summary>Confirms the asynchronous IDBFS flush; memory-file success alone is not durable Web saving.</summary>
    public sealed class BrowserPersistence : MonoBehaviour
    {
        public static event Action<bool> Completed;
        public static event Action StateChanged;
        public static bool Pending { get; private set; }
        static BrowserPersistence instance;
        bool dirty;
        int ticket;
        float deadline;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void Wattshift_SyncPersistentData(string receiver,int ticket);
        [DllImport("__Internal")] static extern void Wattshift_RegisterPageLifecycle(string receiver);
#endif
        public static void Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(instance!=null)return;
            var host=new GameObject("WATTSHIFT browser persistence");DontDestroyOnLoad(host);
            instance=host.AddComponent<BrowserPersistence>();Wattshift_RegisterPageLifecycle(host.name);
#endif
        }
        public static void Request()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Initialize();
            if(Pending){instance.dirty=true;return;}
            Pending=true;StateChanged?.Invoke();instance.ticket++;instance.deadline=Time.realtimeSinceStartup+15;
            try{Wattshift_SyncPersistentData(instance.gameObject.name,instance.ticket);}
            catch(Exception){instance.OnStorageSync(instance.ticket+":error");}
#endif
        }
        public void OnStorageSync(string result)
        {
            if(!Pending || string.IsNullOrEmpty(result))return;
            int split=result.IndexOf(':');
            if(split<0||!int.TryParse(result.Substring(0,split),out int received)||received!=ticket)return;
            Pending=false;
            bool ok=result.Substring(split+1)=="ok";
            if(ok&&dirty){dirty=false;Request();return;}
            dirty=false;Completed?.Invoke(ok);StateChanged?.Invoke();
        }
        public void FlushFromPage(string ignored)
        {
            PersistentSettings.FlushNow();
            var session=GameSession.Instance;
            if(session!=null&&session.Phase!=GamePhase.Menu)session.Save();else Request();
        }
        void Update(){if(Pending&&Time.realtimeSinceStartup>=deadline)OnStorageSync(ticket+":timeout");}
        void OnDestroy(){if(instance==this){instance=null;Pending=false;}}
    }
}
