using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Cinemachine;
using FG.Common;
using FG.Common.Audio;
using FG.Common.CMS;
using FG.Common.Loadables;
using FGClient;
using FGClient.Rendering.XRay;
using FGClient.VictoryScreen;
using FGTools.Config;
using FGTools.HarmonyPatches;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Internal.Extensions;
using FGTools.Services;
using FGTools.States.Logic;
using FGTools.UI;
using FMOD.Studio;
using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem.Dynamic.Utils;
using Levels.Obstacles;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniverseLib.UI;
using static FGTools.Config.Config;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Launcher;
using static FGTools.Services.SpeedrunService;
using static FGTools.States.Logic.FGTStateManager;
using Random = UnityEngine.Random;
namespace FGTools.States
{
    public class InternalState : Logic.FGTState
    {
#if !PROD
        Rect NewBetaWaterRect = new(5, Screen.height - 260, 700, 250);
        readonly bool AllowWatermark = true;
        readonly bool StaticWatermark = true;
        readonly TimeSpan RefreshTime = TimeSpan.FromSeconds(5);
#endif
        public bool LoaderUIToggle = false;
        public Font TargetFont;
        public CustomisationSelections LatestSelections;
        public bool OfflinePatches = false;
        public string LatestError;
        public bool ShouldSkipErrors;
        float TargetTime = 0;
        internal FGToolsUI ToolsUI;

        public override void OnStateSet()
        {
        
        }

        internal static void ResetRandomCosmetics()
        {
            if (StateManager.InternalState.LatestSelections != null)
                GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections = StateManager.InternalState.LatestSelections;

            if (FallGuyBehaviour.Instance != null)
                CustomisationManager.Instance.ApplyCustomisationsToFallGuy(FallGuyBehaviour.Instance.gameObject, StateManager.InternalState.LatestSelections, FGBehaviour.PlayerTeamId);
        }

        internal static void HandleRandomCosmetics()
        {
            if (!StateManager.IsInGameplay)
                return;

            var manager = CustomisationManager.Instance;
            if (manager == null)
                return;

            var cms = CMSLoader.Instance;
            List<string> topIds = [.. cms._costumesUpperSO.CostumesTop.Keys];
            List<string> bottomIds = [.. cms._costumesLowerSO.CostumesBottom.Keys];
            List<string> patternIds = [.. cms._costumesPatternsSO.Patterns.Keys];
            List<string> colorsIds = [.. cms._costumesColourSchemasSO.Colours.Keys];
            List<string> facesIds = [.. cms._costumesFaceplatesSO.Faceplates.Keys];
            List<string> emotesIds = [.. cms._cosmeticsEmoteSO.Emotes.Keys];

            var handler = FGBehaviour.GetComponent<FallguyCustomisationHandler>();
            if (handler == null)
                return;

            handler.UpdateCostumeOption(manager.GetUpperCostumeWithId(topIds[Random.RandomRange(0, topIds.Count)], true), false);
            handler.UpdateCostumeOption(manager.GetLowerCostumeWithId(bottomIds[Random.RandomRange(0, bottomIds.Count)], true), false);
            handler.UpdateColourOption(manager.GetColourOptionWithId(colorsIds[Random.RandomRange(0, colorsIds.Count)], true));
            handler.UpdateFaceplateColours(manager.GetFaceplateOptionWithId(facesIds[Random.RandomRange(0, facesIds.Count)], true));
            handler.UpdatePatternTexture(manager.GetSkinPatternOptionWithId(patternIds[Random.RandomRange(0, patternIds.Count)], true));

            var eTop = manager.GetEmoteOptionWithId(emotesIds[Random.RandomRange(0, emotesIds.Count)], true);
            var eRight = manager.GetEmoteOptionWithId(emotesIds[Random.RandomRange(0, emotesIds.Count)], true);
            var eBottom = manager.GetEmoteOptionWithId(emotesIds[Random.RandomRange(0, emotesIds.Count)], true);
            var eLeft = manager.GetEmoteOptionWithId(emotesIds[Random.RandomRange(0, emotesIds.Count)], true);
          
            List<EmotesOption> emotes = [eTop, eRight, eBottom, eLeft];

            CustomisationSelections sect = GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections;

            int emoteIndex = 0;

            for (int i = 0; i < sect.FirstWheelOptions.Count; i++)
            {
                if (sect.FirstWheelOptions[i].name.Contains("Emote") && emoteIndex < emotes.Count)
                {
                    sect.FirstWheelOptions[i] = emotes[emoteIndex++];
                }
            }

            for (int i = 0; i < sect.SecondWheelOptions.Count; i++)
            {
                if (sect.SecondWheelOptions[i].name.Contains("Emote") && emoteIndex < emotes.Count)
                {
                    sect.SecondWheelOptions[i] = emotes[emoteIndex++];
                }
            }


            XRayUtils.RemoveXRayControllerForCharacter(FGBehaviour.FGCC);
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var activeScene = SceneManager.GetActiveScene().name;

            FMODTool.UnloadAllLoadedBanks();

            if (activeScene != "Transition")
            {
                if (!HarmonyPatched)
                {
                    //Plugin.DoHarmonyPatch();

                    if (FGCHarmonyPatched)
                    {
                        FGCHarmony.UnpatchSelf();
                        FGCHarmonyPatched = false;
                    }
                }

                UpdateColliderView();

                if (FGTServiceManager.GetService<RoundLoaderService>() != null && !FGTServiceManager.GetService<RoundLoaderService>().UsingAdditiveLoad)
                {
                    if (SpeedrunMode.Value)
                        FGTServiceManager.GetService<SpeedrunService>().SpeedrunState = RunState.Inactive;

                    try { AudioMixing.Instance.ResetAllSnapshotParams(); } catch { }

                    StateManager.FGCurrentState = PlayerState.Despawned;
                    StateManager.FGTCurrentState = ToolsState.SceneLoaded;

                    if (CGM != null && CGM._musicInstance != null)
                        FMODTool.EndFmod(CGM._musicInstance._eventInstance, STOP_MODE.ALLOWFADEOUT);

                    FGTServiceManager.GetService<StatisticsService>().ResetTimer();
                }

                if (activeScene == "MainMenu")
                    StateManager.SetState(new MenuState());

                if (activeScene.Contains("Reward_Screen"))
                    StateManager.HandleFGTState(FGTStateManager.ToolsState.Results);

                if (activeScene != "MainMenu" && !activeScene.StartsWith("FallGuy_Fraggle"))
                {
                    if (GravZoneEffect.Value)
                    {
                        foreach (COMMON_GravityModifierVolume gravZone in Resources.FindObjectsOfTypeAll<COMMON_GravityModifierVolume>())
                            gravZone._playAudio = true;
                    }
                }

                if (activeScene.StartsWith("FallGuy_Fraggle"))
                {
                    if (!LocalServerService.IsServerInOperation) return; 

                    if (StateManager.FGTCurrentState != ToolsState.InCreative && !StateManager.IsFGC)
                    {
                        FGTLog(LogLevel.Info, GetType(), "Loading into FGC");
                        StateManager.HandleFGTState(ToolsState.InCreative);
                    }
                    else if (StateManager.IsFGC && StateManager.FGTCurrentState != ToolsState.GPFGCLoading)
                    {
                        FGTLog(LogLevel.Info, GetType(), "FGC Gameplay loading");
                        StateManager.HandleFGTState(ToolsState.GPFGCLoading);
                    }
                }
            }
        }

        public override void DisplayGUI()
        {
           // ApplyGUISkin();

#if DEV || CLOSEDBETA
            if (AllowWatermark)
                BETA_WatermarkGUI();
#endif
            WatermarkGUI();
        }


        public void ApplyGUISkin()
        {
            GUI.skin.font = TargetFont;
            GUI.skin.label.fontSize = (int)(0.0123f * Screen.height);
            GUI.skin.button.fontSize = (int)(0.0123f * Screen.height);
            GUI.skin.toggle.fontSize = (int)(0.0123f * Screen.height);
            GUI.skin.textField.fontSize = (int)(0.0123f * Screen.height);
        }

        public override void UpdateState()
        {
            var guiInst = FGToolsUI.Instance;

#if !PROD
            if (!StaticWatermark)
                TargetTime += Time.unscaledDeltaTime;
#endif
            if (guiInst == null)
                return;

            guiInst?.GUIController();
            if (StateManager.CanUseHotkeys)
            {
                //if (Input.GetKeyDown(KeyCode.F9))
                //    allowWatermark = !allowWatermark;

                if (Input.GetKeyDown(ToggleCusorHotkey.Value))
                {
                    Cursor.lockState = Cursor.visible ? CursorLockMode.Locked : CursorLockMode.None;
                    Cursor.visible = !Cursor.visible;
                }

                if (Input.GetKeyDown(ToggleUIHotkey.Value))
                {
                    LoaderUIToggle = !LoaderUIToggle;
                    guiInst.ToggleUI(LoaderUIToggle);
                }
            }
        }

#if !PROD
        const string WatermarkPlaceholder = "{0} {1} - V{2}\n\nBuild Date: {3}\nBuild ID: {4}\nBuild Commit: #{5}\nChecks State: {6} {7}\nContent Version: {8}\n{9}\nUTC: {10}";
        void BETA_WatermarkGUI()
        {

            if (TargetTime >= RefreshTime.Seconds)
            {
                NewBetaWaterRect.x = Random.Range(0, Screen.width - NewBetaWaterRect.width);
                NewBetaWaterRect.y = Random.Range(0, Screen.height - NewBetaWaterRect.height);
                TargetTime = 0;
            }

            var target = BuildInfoColor;
            target.a -= 0.55f;
            GUIStyle def = new(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = (int)(0.0153f * Screen.height),
                alignment = TextAnchor.LowerLeft,
                normal = { textColor = target },
                font = TargetFont,
               
            };

            GUI.Label(NewBetaWaterRect, string.Format(WatermarkPlaceholder, [Launcher.DisplayName, Launcher.BuildInfo.Config, Launcher.BuildInfo.UI_Version, Launcher.BuildInfo.BuildDate, Launcher.BuildInfo.GUID, Launcher.BuildInfo.GetCommit(), OnlineCheck.ChecksDisplay, OnlineCheck.ReturnChecksGoal(), OnlineCheck.FGTContent?.ContentVersion, Launcher.BuildInfo.GetDefines(), DateTime.UtcNow]), def);
        }
#endif

        void WatermarkGUI()
        {
            var watermark = Config.Config.WatermarkLevel.Value switch
            {
                Watermark.OnlyVersion => $"{Launcher.DisplayName} V{Launcher.BuildInfo.UI_Version}",
                Watermark.None => string.Empty,
                Watermark.VersionAndCredits => $"{Launcher.DisplayName} V{Launcher.BuildInfo.UI_Version} {Description[Description.IndexOf("by")..]}",
                _ => throw new NotImplementedException(),
            };

            GUIStyle upper = new(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = (int)(0.012f * Screen.height),
                font = TargetFont
            };
            GUIStyle lower = new(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = (int)(0.012f * Screen.height),
                normal = { textColor = BuildInfoColor },
                font = TargetFont
            };

            GUI.Label(new Rect((Screen.width - 500f) / 2f, Screen.height - 25f, 500, 25), $"<b>{watermark}</b>", lower);
            GUI.Label(new Rect((Screen.width - 500f) / 2f, Screen.height - 25f - 2f, 500, 25), $"<b>{watermark}</b>", upper);
        }

        public override void OnStateExit()
        {

        }
    }
}
