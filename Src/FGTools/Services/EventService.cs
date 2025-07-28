using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx.Logging;
using FGTools.Services.Logic;
using static FGTools.Internal.Extensions.FLZ_Extensions;

namespace FGTools.Services
{
    internal class EventService : FGTService
    {
        public class FGT_EventList
        {
            public Dictionary<string, string> StringEvents { get; set; }
            public Dictionary<string, bool> BoolEvents { get; set; }
            public Dictionary<string, DateTime> ScheduledEvents { get; set; }
        }

        private readonly string[] StringDefinitions = ["MenuEntranceVersion", "LatestUserPreset"];
        private readonly string[] BoolDefinitions = ["Pirated", "MeetPingas", "MeetPingasAgain", "FFMTip", "FCTip", "SpeedrunnerAlert", "OldSp", "RpcFGCAlert"];
        private readonly string[] ScheduledDefinitions = ["ExploreDownloadSchedule"];

        FGT_EventList EventList;
        public override void RegisterService()
        {
            RegisterEvents(false);
        }

        public bool ReturnBoolEventValue(string evt)
        {
            if (CheckIfEventRegistred(evt))
                return EventList.BoolEvents[evt];
            else
                return false;
        }

        public string ReturnStringEventValue(string evt)
        {
            if (CheckIfEventRegistred(evt))
                return EventList.StringEvents[evt];
            else
                return string.Empty;
        }

        public DateTime ReturnScheduledEventValue(string evt)
        {
            if (CheckIfEventRegistred(evt))
                return EventList.ScheduledEvents[evt];
            else
                return DateTime.MinValue;
        }

        public void SetEventValue(string evt, bool value)
        {
            if (CheckIfEventRegistred(evt))
            {
                EventList.BoolEvents[evt] = value;
                WriteSave();
            }
        }

        public void SetEventValue(string evt, string value)
        {
            if (CheckIfEventRegistred(evt))
            {
                EventList.StringEvents[evt] = value;
                WriteSave();
            }
        }

        public void SetEventValue(string evt, DateTime value)
        {
            if (CheckIfEventRegistred(evt))
            {
                EventList.ScheduledEvents[evt] = value;
                WriteSave();
            }
        }

        public bool CheckIfEventIsExpired(string evt)
        {
            if (DateTime.UtcNow >= ReturnScheduledEventValue(evt))
                return true;
            else
                return false;
        }

        bool CheckIfEventRegistred(string evt)
        {
            if (StringDefinitions.Contains(evt) || BoolDefinitions.Contains(evt) || ScheduledDefinitions.Contains(evt))
                return true;
            else
            {
                FGTLog(LogLevel.Warning, base.GetType(), $"Event {evt} not registred!");
                return false;
            }
        }

        public void RegisterEvents(bool cleanup)
        {
            EventList = new()
            {
                StringEvents = [],
                BoolEvents = [],
                ScheduledEvents = []
            };

            FGT_EventList latestList = null;
            if (File.Exists(Plugin.EventsListNew))
            {
                if (cleanup)
                    File.Delete(Plugin.EventsListNew);
                else
                    latestList = JsonSerializer.Deserialize<FGT_EventList>(File.ReadAllText(Plugin.EventsListNew));
            }

            foreach (var str in StringDefinitions)
            {
                EventList.StringEvents.Add(str, "");
                if (latestList != null && latestList.StringEvents.ContainsKey(str))
                    EventList.StringEvents[str] = latestList.StringEvents[str];

            }

            foreach (var boolean in BoolDefinitions)
            {
                EventList.BoolEvents.Add(boolean, false);
                if (latestList != null && latestList.BoolEvents.ContainsKey(boolean))
                    EventList.BoolEvents[boolean] = latestList.BoolEvents[boolean];
            }

            foreach (var scheduled in ScheduledDefinitions)
            {
                EventList.ScheduledEvents.Add(scheduled, new DateTime(0));
                if (latestList != null && latestList.ScheduledEvents.ContainsKey(scheduled))
                    EventList.ScheduledEvents[scheduled] = latestList.ScheduledEvents[scheduled];
            }

            WriteSave();

            FGTLog(LogLevel.Info, base.GetType(), "Events registred");
        }

        void WriteSave() => File.WriteAllText(Plugin.EventsListNew, JsonSerializer.Serialize<FGT_EventList>(EventList));

        public override void UpdateService()
        {
        }
        public override void DrawGUI()
        {
        }
    }
}
