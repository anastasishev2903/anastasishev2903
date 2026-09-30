using System;
using System.Collections.Generic;
using UnityEngine;

namespace AndroidInstallReferrer
{
    public class InstallReferrerData
    {
        public string InstallReferrer
        {
            get;
        }

        public string InstallVersion
        {
            get;
        }
        
        public bool GooglePlayInstant
        {
            get;
        }

        public DateTime InstallBeginTime
        {
            get;
        }
        
        public DateTime InstallBeginServerTime
        {
            get;
        }

        public DateTime ReferrerClickTime
        {
            get;
        }
        
        public DateTime ReferrerClickServerTime
        {
            get;
        }

        public string Error
        {
            get;
        }

        public bool IsSuccess
        {
            get;
        }
        
        //Success
        public InstallReferrerData(string installReferrer, string installVersion, bool googlePlayInstant, 
            long installBeginTimestampSeconds, long installBeginTimestampServerSeconds,
            long referrerClickTimestampSeconds, long referrerClickTimestampServerSeconds)
        {
            InstallReferrer = installReferrer;
            InstallVersion = installVersion;
            GooglePlayInstant = googlePlayInstant;
            InstallBeginTime = TimestampToDateTime(installBeginTimestampSeconds);
            InstallBeginServerTime = TimestampToDateTime(installBeginTimestampServerSeconds);
            ReferrerClickTime = TimestampToDateTime(referrerClickTimestampSeconds);
            ReferrerClickServerTime = TimestampToDateTime(referrerClickTimestampServerSeconds);
            IsSuccess = true;
        }
        
        //Success
        public InstallReferrerData(string installReferrer, string installVersion, bool googlePlayInstant, 
            DateTime installBeginTimestampSeconds, DateTime installBeginTimestampServerSeconds,
            DateTime referrerClickTimestampSeconds, DateTime referrerClickTimestampServerSeconds)
        {
            InstallReferrer = installReferrer;
            InstallVersion = installVersion;
            GooglePlayInstant = googlePlayInstant;
            InstallBeginTime = installBeginTimestampSeconds;
            InstallBeginServerTime = installBeginTimestampServerSeconds;
            ReferrerClickTime = referrerClickTimestampSeconds;
            ReferrerClickServerTime = referrerClickTimestampServerSeconds;
            IsSuccess = true;
        }

        //Error
        public InstallReferrerData(string error)
        {
            IsSuccess = false;
            Error = error;
        }

        public string[] SplitParameters()
        {
            return InstallReferrer.Split('&', '?');
        }

        public Dictionary<string, string> ParseParameters()
        {
            var splittedParams = SplitParameters();
            var dictionary = new Dictionary<string, string>();
            foreach (var param in splittedParams)
            {
                var parameterWithValue = param.Split('=');
                if (parameterWithValue.Length == 2)
                {
                    dictionary.Add(parameterWithValue[0], parameterWithValue[1]);
                }
                else
                {
                    Debug.LogWarning("Cannot parse parameter: " + param);
                }
            }
            return dictionary;
        }

        private DateTime TimestampToDateTime(long timestamp)
        {
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0,0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(timestamp).ToLocalTime();
            return dateTime;
        }
    }
}