using System;
using UnityEngine;

namespace AndroidInstallReferrer
{
    public class InstallReferrerProxy : AndroidJavaProxy
    {
        public Action<InstallReferrerData> Callback;
        
        public InstallReferrerProxy() : base("com.unitymedved.installreferrer.InstallReferrerCallback")
        {
        }

        private void Success(AndroidJavaObject javaObject)
        {
            var installReferrer = javaObject.Get<string>("referrer");
            var version = javaObject.Get<string>("version");
            var googlePlayInstant = javaObject.Get<bool>("googlePlayInstant");
            var installBeginTimestampSeconds = javaObject.Get<long>("installBeginTimestampSeconds");
            var installBeginTimestampServerSeconds = javaObject.Get<long>("installBeginTimestampServerSeconds");
            var referrerClickTimestampSeconds = javaObject.Get<long>("referrerClickTimestampSeconds");
            var referrerClickTimestampServerSeconds = javaObject.Get<long>("referrerClickTimestampServerSeconds");

            Callback(new InstallReferrerData(installReferrer, version, googlePlayInstant, 
                installBeginTimestampSeconds, installBeginTimestampServerSeconds,
                referrerClickTimestampSeconds, referrerClickTimestampServerSeconds));
        }

        private void Error(string error)
        {
            Callback(new InstallReferrerData(error));
        }
    }
}