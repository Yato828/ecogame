using UnityEngine;
namespace Wattshift
{
    /// <summary>Debounces slider writes and flushes at explicit lifecycle points, not only application quit.</summary>
    public sealed class PersistentSettings : MonoBehaviour
    {
        static PersistentSettings instance;
        static bool pending;
        float at;
        public static void Request()
        {
            if(!Application.isPlaying){PlayerPrefs.Save();return;}
            if(instance==null){var host=new GameObject("WATTSHIFT settings persistence");DontDestroyOnLoad(host);instance=host.AddComponent<PersistentSettings>();}
            pending=true;instance.at=Time.realtimeSinceStartup+.25f;
        }
        public static void FlushNow(){if(!pending)return;PlayerPrefs.Save();pending=false;BrowserPersistence.Request();}
        void Update(){if(pending&&Time.realtimeSinceStartup>=at)FlushNow();}
        void OnApplicationFocus(bool focused){if(!focused)FlushNow();}
        void OnApplicationPause(bool paused){if(paused)FlushNow();}
        void OnDestroy(){if(instance==this){FlushNow();instance=null;}}
    }
}
