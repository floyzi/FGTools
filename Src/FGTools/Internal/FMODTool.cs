using FG.Common.Definition;
using FGTools.Internal.Behaviours;
using FGTools.States.Logic;
using FMOD.Studio;
using FMODUnity;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

        internal static bool TryGetEventInstance(string key, out EventInstance evt)
        {
            evt = default;
            if (ValidEvents.TryGetValue(key, out var eventInstance) && eventInstance.Event.hasHandle())
            {
                evt = eventInstance.Event;
                return true;
            }

            return false;
        }

        internal static void UnloadBank(string bankName)
        {
            var existing = FGTStateManager.Instance.gameObject.GetComponent<SoundBankLoader>();
            if (existing != null)
            {
                GameObject.DestroyImmediate(existing?._soundbanksToLoad);
                GameObject.DestroyImmediate(existing);
            }

            var l = FGTStateManager.Instance.gameObject.AddComponent<SoundBankLoader>();
            l._soundbanksToLoad = ScriptableObject.CreateInstance<SceneSoundBanksSO>();
            l._soundbanksToLoad.SoundBanksToLoad = new([bankName]);
            l.UnloadBanks();
            GameObject.DestroyImmediate(l._soundbanksToLoad);
            GameObject.DestroyImmediate(l);
        }

        internal static void LoadBank(string bankName) 
        {
            var existing = FGTStateManager.Instance.gameObject.GetComponent<SoundBankLoader>();
            if (existing != null)
            {
                GameObject.DestroyImmediate(existing?._soundbanksToLoad);
                GameObject.DestroyImmediate(existing);
            }

            var l = FGTStateManager.Instance.gameObject.AddComponent<SoundBankLoader>();
            l._soundbanksToLoad = ScriptableObject.CreateInstance<SceneSoundBanksSO>();
            l._soundbanksToLoad.SoundBanksToLoad = new([bankName]);
            l.LoadBanks();
            GameObject.DestroyImmediate(l._soundbanksToLoad);
            GameObject.DestroyImmediate(l);
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
