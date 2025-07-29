using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
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
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Plugin;
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
        internal FGToolsUI.NewGUI ToolsUI;

        public override void OnStateSet()
        {
        
        }

        internal static void ResetRandomCosmetics()
        {
            if (StateManager.InternalState.LatestSelections != null)
                GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections = StateManager.InternalState.LatestSelections;

            if (FallGuyBehaviour._instance != null)
            {
                CustomisationManager.Instance.ApplyCustomisationsToFallGuy(FallGuyBehaviour._instance.FallGuy, StateManager.InternalState.LatestSelections, FGTStateManager.FGBehaviour.PlayerTeamId);
            }
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
            string activeScene = SceneManager.GetActiveScene().name;
            FMODTool.UnloadAllLoadedBanks(FMODTool.UnloadParam.UnloadOnNewScene);
            if (activeScene != "Transition")
            {
                if (!Plugin.HarmonyPatched)
                {
                    //Plugin.DoHarmonyPatch();

                    if (Plugin.FGCHarmonyPatched)
                    {
                        Plugin.FGCHarmony.UnpatchSelf();
                        Plugin.FGCHarmonyPatched = false;
                    }
                }

                if (ColliderView.Value)
                {
                    foreach (Camera cam in Resources.FindObjectsOfTypeAll<Camera>())
                    {
                        if (cam.gameObject.GetComponent<ColliderCameraView>() == null)
                            cam.gameObject.AddComponent<ColliderCameraView>();
                    }
                }
                if (FGTServiceManager.GetService<RoundLoaderService>() != null && !FGTServiceManager.GetService<RoundLoaderService>().UsingAdditiveLoad)
                {
                    if (SpeedrunMode.Value)
                    {
                        //if (activeScene != "Fallguy_Victory_Scene")
                        //    ServiceManagerFGT.GetService<SpeedrunService>().ResetStats();
                        FGTServiceManager.GetService<SpeedrunService>().SpeedrunState = RunState.Inactive;
                    }
                    try { AudioMixing.Instance.ResetAllSnapshotParams(); } catch { }
                    StateManager.HaveActivePopup = false;
                    StateManager.FGCurrentState = PlayerState.Despawned;
                    StateManager.FGTCurrentState = FGTStateManager.FGTState.SceneLoaded;
                    AttackOfTheTime.Reset();

                    if (CGM != null && CGM._musicInstance != null)
                        FMODTool.EndFmod(CGM._musicInstance._eventInstance, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);

                    FGTServiceManager.GetService<StatisticsService>().ResetTimer();
                }
                else
                {
                    GameObject lights = GameObject.Find("----------------LIGHTS");
                    lights?.SetActive(false);
                    StateManager.RoundLoadingAllowed = false;
                }

                if (activeScene == "MainMenu")
                {
                    StateManager.SetState(new MenuState());
                    StateManager.GetState<MenuState>().menuComplete = false;
                }

                if (activeScene.Contains("Reward_Screen"))
                    StateManager.HandleFGTState(FGTStateManager.FGTState.Results);

                if (activeScene != "MainMenu" && !activeScene.StartsWith("FallGuy_Fraggle"))
                {
                    if (GravZoneEffect.Value)
                        foreach (COMMON_GravityModifierVolume gravZone in Resources.FindObjectsOfTypeAll<COMMON_GravityModifierVolume>())
                            gravZone._playAudio = true;
                }
                if (activeScene.StartsWith("FallGuy_Fraggle"))
                {
                    if (StateManager.FGTCurrentState != FGTStateManager.FGTState.InCreative && !StateManager.IsFGC)
                    {
                        FGTLog(LogLevel.Info, base.GetType(), "Loading into FGC");
                        StateManager.HandleFGTState(FGTStateManager.FGTState.InCreative);
                    }
                    else if (StateManager.IsFGC && StateManager.FGTCurrentState != FGTStateManager.FGTState.GPFGCLoading)
                    {
                        FGTLog(LogLevel.Info, base.GetType(), "FGC Gameplay loading");
                        StateManager.HandleFGTState(FGTStateManager.FGTState.GPFGCLoading);
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
            var guiInst = FGToolsUI.NewGUI.Instance;

            if (!StaticWatermark)
                TargetTime += Time.unscaledDeltaTime;

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

                var isInLocker = FGTServiceManager.GetService<CosmeticsService>().IsSomeScreenActive();
                if (Input.GetKeyDown(ToggleUIHotkey.Value))
                {
                    if (isInLocker)
                    {
                        FLZ_Extensions.CreateNotification("not here", "you can't toggle UI in locker", FGT_Warning_Color);
                        return;
                    }

                    LoaderUIToggle = !LoaderUIToggle;
                    guiInst.UIRoot.gameObject.SetActive(LoaderUIToggle);

                    if (guiInst.TabHover)
                    {
                        guiInst.TabHover = false;
                        guiInst.TabHoverText.text = null;
                    }

                    UniversalUI.SetUIActive(UniverseGUID, LoaderUIToggle);
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
            target.a -= 0.7f;
            GUIStyle def = new(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = (int)(0.0133f * Screen.height),
                alignment = TextAnchor.LowerLeft,
                normal = { textColor = target },
                font = TargetFont,
               
            };

            GUI.Label(NewBetaWaterRect, string.Format(WatermarkPlaceholder, [Plugin.DisplayName, Plugin.BuildInfo.Config.ToUpper(), Plugin.BuildInfo.UI_Version, Plugin.BuildInfo.BuildDate, Plugin.BuildInfo.GUID, Plugin.BuildInfo.GetCommit(), OnlineCheck.ChecksDisplay, OnlineCheck.ReturnChecksGoal(), OnlineCheck.FGTContent?.ContentVersion, Plugin.BuildInfo.GetDefines(), DateTime.UtcNow]), def);
        }
#endif

        void WatermarkGUI()
        {
            var watermark = ConfigManager.WatermarkLevel.Value switch
            {
                Watermark.OnlyVersion => $"{Plugin.DisplayName} V{Plugin.BuildInfo.UI_Version}",
                Watermark.None => string.Empty,
                Watermark.VersionAndCredits => $"{Plugin.DisplayName} V{Plugin.BuildInfo.UI_Version} {Description[Description.IndexOf("by")..]}",
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

        [HideFromIl2Cpp]
        public IEnumerator PlayVictoryAnim(VictoryScreenViewModel player)
        {
            yield return new WaitForSeconds(0.1f);
            GameObject go = AddressableAssetManager.Instance.LoadAsset<GameObject>(GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections.VictoryPoseOption.AnimationPrefabRef.AssetGUID);
            if (go != null)
            {
                var screen = player.CreateVictoryAnimationProp(go);
                PlayerMetadata metadata = new(GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections, FGBehaviour.PlayerTeamId, GlobalGameStateClient.Instance.PlayerProfile.PlatformAccountName, ClientBuildDetails.Platform, false);
                player.ConfigureWinnersText(player._localisedStrings.GetString("winner"), Color.white, player._winnerTextOutlineColor);
                player.SetupSkipPromptRoutine();
                VictoryScreenViewModel.CharacterData data;
                Func<GameObject, bool> value = x => x.name == "FallGuy";
                GameObject prefab = player._config.GetAllLoadedNetworkPrefabs().Find(value);
                GameObject fallGuy = UnityEngine.Object.Instantiate<GameObject>(prefab, player._fallguySpawnPosition, true);
                fallGuy.transform.localPosition = Vector3.zero;
                fallGuy.transform.localRotation = Quaternion.identity;
                data = new VictoryScreenViewModel.CharacterData(fallGuy);
                Rigidbody fgRb = fallGuy.GetComponent<Rigidbody>();
                Transform fgRagdoll = fallGuy.transform.Find("Ragdoll");
                FallGuysCharacterControllerInput controllerInput = fallGuy.GetComponentInChildren<FallGuysCharacterControllerInput>();
                data.CustomisationHandler.SetupForVictoryScreen();
                fgRb.isKinematic = true;
                data.CharacterController.IsControlledLocally = false;
                data.CharacterController.enabled = false;
                controllerInput.enabled = false;
                fgRagdoll.gameObject.SetActive(false);
                data.CharacterController.MotorAgent.SetActive(false);
                player.CustomiseFallguy(fallGuy, data.GeoChildren, metadata.Selections, metadata.TeamId);
                WinnerInfo winnerInfo = player._winnersInfos.AddWinner(metadata, new(), data.Animator, 0, player._fallguySpawnPosition, true);
                winnerInfo.UpdateNameplate();
                var victoryOption = AddressableAssetManager.Instance.LoadAsset<AnimationClip>(GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections.VictoryPoseOption.clipAssetRef.AssetGUID);
                data.Animator.runtimeAnimatorController.animationClips.AddLast<AnimationClip>(victoryOption);
                Func<AnimationClip, bool> value1 = x => x.name == "FG_Victory_DefaultGuy";
                var defanim = data.Animator.runtimeAnimatorController.animationClips.Find(value1);
                Debug.Log(data.Animator.runtimeAnimatorController.animationClips.Last().name);
                Debug.Log(victoryOption.name);
                AnimatorOverrideController animatorOverrideController = new(winnerInfo.Animator.runtimeAnimatorController);
                animatorOverrideController[defanim] = victoryOption;
                AnimatorOverrideController overrideController = animatorOverrideController;
                winnerInfo.Animator.runtimeAnimatorController = overrideController;
                //FMODTool.PlayFMODEvent(GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections.VictoryPoseOption.AudioEvent, FMODTool.UnloadParam.UnloadOnNewScene);
                screen.SetActive(true);
                winnerInfo.Animator.Play("FG_Victory_DefaultGuy");
                winnerInfo.Animator.Update(0f);
                player._victoryAnimFinishedTimestamp = Time.time + victoryOption.length;

                //FMODTool.GetEvent(GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections.VictoryPoseOption.AudioEvent).Start();
            }
            else
            {
                FGTLog(LogLevel.Info, base.GetType(), "Unable to play victory animation, fallback to menu");
                LeaveMatchPopupManager.Instance.OnClose(true);
            }
        }

        public override void OnStateExit()
        {

        }
    }
}
