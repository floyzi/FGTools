using FMOD.Studio;
using FMODUnity;
using NAudio.Codecs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using static FGTools.Internal.Extensions.FLZ_Extensions;
namespace FGTools.Internal
{
    internal static class FMODTool
    {
        public struct FMODEvent
        {
            public string EventName;
            public FMOD.GUID EventGuid;
            public EventInstance Event;
        }

        static readonly Dictionary<string, FMODEvent> ValidEvents = [];

        internal static void UnloadBank(string bankName)
        {
            RuntimeManager.UnloadBank(bankName);
            RuntimeManager.UnloadBank($"{bankName}.assets");
        }

        internal static void LoadBank(string bankName) 
        {
            foreach (var bank in AudioManager.Instance._fmodData.SoundBanksArray.ToList().FindAll(x => x.Name == bankName || x.Name == bankName + ".assets"))
            {
                var res = RuntimeManager.LoadBank(bank.AssetReference, true);
            }
        }

        internal static void UnloadAllLoadedBanks()
        {
            for (int i = ValidEvents.Count - 1; i >= 0; i--)
            {
                var evt = ValidEvents.ElementAt(i);
                evt.Value.Event.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                evt.Value.Event.release();
            }

            ValidEvents.Clear();
        }

        internal static bool CreateFMODEvent(string eventName, out EventInstance res)
        {
            if (ValidEvents.TryGetValue(eventName, out var cachedEvt))
                ValidEvents.Remove(eventName);

            FGTLog(BepInEx.Logging.LogLevel.Info, "CreateFMODEvent", $"Creating {eventName}...");

            var evtGuid = AudioManager.GetGuidForKey(eventName);
            if (evtGuid == default)
            {
                FGTLog(BepInEx.Logging.LogLevel.Error, "CreateFMODEvent", $"Event {eventName} doesn't have a valid GUID");
                res = default;
                return false;
            }

            try
            {
                res = RuntimeManager.CreateInstance(evtGuid);

                if (!res.hasHandle() || !res.isValid())
                {
                    FGTLog(BepInEx.Logging.LogLevel.Error, "CreateFMODEvent", $"Event {eventName} created with invalid handle");
                    res = default;
                    return false;
                }

                ValidEvents.Add(eventName, new()
                {
                    Event = res,
                    EventName = eventName,
                    EventGuid = evtGuid,
                });


                return true;
            }
            catch (Exception ex) 
            {
                FGTLog(BepInEx.Logging.LogLevel.Error, "CreateFMODEvent", $"FAILED FOR {eventName}\n\n{ex}");
                res = default;
                return false;
            }
        }

        //this code is ass, session terminated
        public static void PlayFMODEvent(string eventName)
        {
            if (ValidEvents.TryGetValue(eventName, out var cachedEvt))
            {
                if (cachedEvt.Event.hasHandle())
                {
                    cachedEvt.Event.start();
                    return;
                }

                ValidEvents.Remove(eventName);
            }

            var banks = AudioManager.Instance._fmodData.GetEventBanks(eventName);
            int neededBanks = banks == null || banks.BankNames == null ? 0 : banks.BankNames.Length;

            if (neededBanks > 0)
            {
                foreach (var bank in banks.BankNames)
                {
                    if (!RuntimeManager.HasBankLoaded(bank))
                    {
                        LoadBank(bank);
                    }
                }
            }
        }

        public static void EndFmod(EventInstance evt, FMOD.Studio.STOP_MODE mode) => evt.stop(mode);
    }
}
