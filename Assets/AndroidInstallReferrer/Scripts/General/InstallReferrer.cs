using System;
using UnityEngine;

namespace AndroidInstallReferrer
{
    public static class InstallReferrer
    {
        public static void GetReferrer(Action<InstallReferrerData> callback)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var currentActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
            var installReferrer = new AndroidJavaObject("com.unitymedved.installreferrer.InstallReferrer");
            var installReferrerProxy = new InstallReferrerProxy {Callback = callback};
            installReferrer.Call("GetInstallReferrerData", new object[] { currentActivity, installReferrerProxy });
#endif
        }
    }
}