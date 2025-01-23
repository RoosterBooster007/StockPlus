using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using UnityEngine;

namespace StockPlus
{
    public class GASession
    {
        private string modId;
        private string modName;
        private string modVersion;
        private string measurementId;
        private string screenTitle;
        private string renewScreenTitle;
        private bool sendSystemInfo;
        private int sessionLengthMins;
        private HttpClient HTTPclient = new HttpClient();
        private DateTimeOffset sessionStartTime = DateTimeOffset.UtcNow;
        private DateTimeOffset lastEng = DateTimeOffset.UtcNow;
        private int GAHitCount = 1;
        private int GASessionCount = 1;
        private string GASessionID = Regex.Replace(System.Guid.NewGuid().ToString(), "[^\\d]", "").Substring(0, 10);

        internal GASession(string modId, string modName, string modVersion, string measurementId, string screenTitle, string renewScreenTitle, bool sendSystemInfo, int sessionLengthMins)
        {
            this.modId = modId;
            this.modName = modName;
            this.modVersion = modVersion;
            this.measurementId = measurementId;
            this.screenTitle = screenTitle;
            this.renewScreenTitle = renewScreenTitle;
            this.sendSystemInfo = sendSystemInfo;
            this.sessionLengthMins = sessionLengthMins;

            if (isFirstRun())
            {
                SendUserGA("page_view", true, "&_fv=1");
            }
            else
            {
                SendUserGA("page_view", true, "");
            }
        }

        private static string getSystemArch()
        {
            if (Environment.Is64BitProcess)
            {
                return "x86_64";
            }
            else
            {
                return "x86";
            }
        }

        private static string getSystemBits()
        {
            if (Environment.Is64BitProcess)
            {
                return "64";
            }
            else
            {
                return "32";
            }
        }

        private bool isFirstRun()
        {
            if (!PlayerPrefs.HasKey(modId + ".newUser"))
            {
                PlayerPrefs.SetInt(modId + ".newUser", 1);
                PlayerPrefs.Save();
                return true;
            }
            return false;
        }

        private bool recentlyUpdated()
        {
            if (PlayerPrefs.GetString(modId + ".v") != modVersion)
            {
                PlayerPrefs.SetString(modId + ".v", modVersion);
                PlayerPrefs.Save();
                return true;
            }
            return false;
        }
        private void startNewGASession()
        {
            GASessionCount++;
            GASessionID = Regex.Replace(System.Guid.NewGuid().ToString(), "[^\\d]", "").Substring(0, 10);
            sessionStartTime = DateTimeOffset.UtcNow;
            lastEng = DateTime.UtcNow;
            GAHitCount = 1;
            SendUserGA("page_view", false, "");
        }

        public void sendGAEvent(string event_category, string event_name, Dictionary<string, string> event_params, bool isEngaged = true)
        {
            if (StockPlusPlugin.telemetryEnabled.Value)
            {
                if ((DateTime.UtcNow - lastEng).TotalMinutes >= sessionLengthMins)
                {
                    startNewGASession();
                }

                event_name = WebUtility.UrlEncode(event_name);
                event_category = WebUtility.UrlEncode(event_category);

                string e_params = "";
                foreach (KeyValuePair<string, string> kvp in event_params)
                {
                    if (int.TryParse(kvp.Value, out int value))
                    {
                        e_params += "&epn." + WebUtility.UrlEncode(kvp.Key) + "=" + value;
                    }
                    else
                    {
                        e_params += "&ep." + WebUtility.UrlEncode(kvp.Key) + "=" + WebUtility.UrlEncode(kvp.Value);
                    }
                }

                string engagedTime = "";
                if (isEngaged)
                {
                    engagedTime += "&_et = " + (long)Math.Floor((DateTime.UtcNow - lastEng).TotalMilliseconds);
                }

                GAHitCount++;
                _ = HTTPclient.GetAsync("https://www.google-analytics.com/g/collect?v=2&tid=" + measurementId + "&cid=" + SystemInfo.deviceUniqueIdentifier + "&sid=" + GASessionID + "&ul=" + CultureInfo.CurrentCulture.Name + "&sr=" + Screen.currentResolution.width + "x" + Screen.currentResolution.height + "&uap=" + WebUtility.UrlEncode(Environment.OSVersion.Platform.ToString()) + "&uam=" + WebUtility.UrlEncode(Application.version) + "&uapv=" + WebUtility.UrlEncode(modVersion) + "&en=" + event_name + "&ep.category=" + event_category + e_params + engagedTime + "&tfd=" + (long)Math.Floor((DateTime.UtcNow - sessionStartTime).TotalMilliseconds) + "&uamb=0&uaw=0&pscdl=noapi&_s=" + GAHitCount + "&sct=" + GASessionCount + "&seg=1&_ee=1&npa=0&dma=0&frm=0&are=1");

                if (isEngaged)
                {
                    lastEng = DateTime.UtcNow;
                }
            }
        }

        private void SendUserGA(string e, bool startingSession, string args)
        {
            if (StockPlusPlugin.telemetryEnabled.Value)
            {
                string sysInfo = "";
                string eventTitle = "";

                if (sendSystemInfo)
                {
                    sysInfo += "&ep.cpu=" + SystemInfo.processorType + "&ep.gpu=" + SystemInfo.graphicsDeviceName;
                }

                if (startingSession)
                {
                    eventTitle = screenTitle;
                }
                else
                {
                    eventTitle = renewScreenTitle;
                }

                _ = HTTPclient.GetAsync("https://www.google-analytics.com/g/collect?v=2&tid=" + measurementId + "&cid=" + SystemInfo.deviceUniqueIdentifier + "&sid=" + GASessionID + "&ul=" + CultureInfo.CurrentCulture.Name + "&sr=" + Screen.currentResolution.width + "x" + Screen.currentResolution.height + "&uap=" + WebUtility.UrlEncode(Environment.OSVersion.Platform.ToString()) + "&uam=" + WebUtility.UrlEncode(Application.version) + "&uapv=" + WebUtility.UrlEncode(modVersion) + "&en=" + e + sysInfo + "&tfd=" + (long)Math.Floor((DateTime.UtcNow - sessionStartTime).TotalMilliseconds) + "&uaa=" + getSystemArch() + "&uab=" + getSystemBits() + "&uamb=0&uaw=0&dt=" + WebUtility.UrlEncode(eventTitle + " (v" + modVersion + ")") + "&are=1&frm=0&pscdl=noapi&seg=1&npa=0&_s=1&sct=" + GASessionCount + "&dma=0&_ss=1&_nsi=1&_ee=1" + args);
            }
        }
    }
}