using System;
using System.Collections.Generic;
using System.Linq;
using FMOD.Studio;
using FMODUnity;
using UnityEngine.AddressableAssets;
using static FGTools.Internal.Extensions.FLZ_Extensions;
namespace FGTools.Internal
{
    public static class FMODTool
    {
        public enum UnloadParam
        {
            Default,
            UnloadOnNewScene
        }
        public struct FMODEventParams
        {
            public string EventName;
            public EventInstance Event;
            public UnloadParam UnloadType;
        }

        static readonly Dictionary<string, AssetReference> LoadedBanks = [];
        static readonly List<FMODEventParams> ValidEvents = [];

        public static void UnloadBank(string bankName)
        {
            foreach (var bank in LoadedBanks)
            {
                if (bank.Key == bankName || bank.Key == bankName + ".assets")
                {
                    RuntimeManager.UnloadBank(bank.Value);
                    LoadedBanks.Remove(bank.Key);
                }
            }
        }

        public static void LoadBank(string bankName, Action onceLoaded = null) 
        {
            foreach (var bank in AudioManager.Instance._fmodData.SoundBanksArray.ToList().FindAll(x => x.Name == bankName || x.Name == bankName + ".assets"))
            {
                RuntimeManager.LoadBank(bank.AssetReference, true, true, new Action(() =>
                {
                    LoadedBanks.Add(bank.Name, bank.AssetReference);
                    onceLoaded?.Invoke();
                }));
            }
        }

        public static void UnloadAllLoadedBanks(UnloadParam param)
        {
            var targetedEvents = ValidEvents.FindAll(x => x.UnloadType == param);

            for (int i = targetedEvents.Count - 1; i >= 0; i--)
            {
                var evt = targetedEvents[i];
                evt.Event.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                evt.Event.release();
                targetedEvents.RemoveAt(i);
            }

            //Broadcaster.Instance.Broadcast(new UnloadSoundBanksEvent(LoadedBanks.Keys.ToArray()));

            ValidEvents.Clear();
            LoadedBanks.Clear();
        }

        //this code is ass, session terminated
        public static void PlayFMODEvent(string eventName, UnloadParam unloadParam, Action<EventInstance> onCreation = null)
        {  
            var banks = AudioManager.Instance._fmodData.GetEventBanks(eventName);
            int loadedBanks = 0;
            int neededBanks = banks._bankNames.Length + banks._bankNames.Length;

            Action OnLoadedBank = () =>
            {
                loadedBanks++;

                if (loadedBanks == neededBanks)
                {
                    FGTLog(BepInEx.Logging.LogLevel.Info, "CreateFMODEvent", $"Creating {eventName}...");
                    var e = RuntimeManager.CreateInstance(AudioManager.GetGuidForKey(eventName));
                    onCreation?.Invoke(e);
                    if (!e.hasHandle() || !e.isValid())
                    {
                        FGTLog(BepInEx.Logging.LogLevel.Error, "CreateFMODEvent", $"Event {eventName} created with invalid handle");
                        return; 
                    }
                    e.start();
                    ValidEvents.Add(new()
                    {
                        Event = e,
                        EventName = eventName,
                        UnloadType = unloadParam
                    });
                }
            };

            foreach (var bank in banks._bankNames)
            {
                if (!RuntimeManager.HasBankLoaded(bank))
                    LoadBank(bank, new(() =>
                    {
                        OnLoadedBank();
                    }));
                else
                    OnLoadedBank();
            }
        }

        public static void EndFmod(EventInstance evt, FMOD.Studio.STOP_MODE mode)
        {
            evt.stop(mode);
        }
    }
}
