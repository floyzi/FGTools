using BepInEx.Logging;
using FG.Common;
using FG.Common.Character;
using FG.Common.CMS;
using FGClient;
using FGClient.Rendering.XRay;
using FGClient.UI;
using FGTools.Content;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Services;
using FGTools.States.Logic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Levels.Progression;
using Spine;
using SRF;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using static FGTools.Config.Config;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.States.Logic.FGTStateManager;
using Random = UnityEngine.Random;

namespace FGTools.States
{
    public class GameplayState : FGTState
    {
        internal FGTController Controller;
        internal SocialPrimeHandler PrimeHandler;
        private float skipIntroHold;
        private bool holdingSkipIntro;
        public GameObject Spawnpoint;

        public override void OnStateSet()
        {
            PrimeHandler = Resources.FindObjectsOfTypeAll<SocialPrimeHandler>().FirstOrDefault();

            var controller = new GameObject($"{Launcher.DisplayName}_Controller");
            Controller = controller.AddComponent<FGTController>();

            GameActions.OnRoundStarts += OnGameplayBegins;
            GameActions.OnCheckpointReached += OnCheckpoint;
        }

        void OnCheckpoint(MPGNetObject mpg, CheckpointZone zone)
        {
            if (!mpg.IsFallGuy || !mpg.FGCharacterController.IsLocalPlayer || !FGTServiceManager.GetService<SpeedrunService>().IsSepeedrunsDisabled)
                return;

            zone.GetNextSpawnPositionAndRotation(out var pos, out var rot);
            Spawnpoint.transform.SetPositionAndRotation(pos, rot);
        }

        void OnGameplayBegins()
        {
            StateManager.RoundLoadingAllowed = true;

            if (!StateManager.IsFGC)
                StateManager.HandleFGTState(ToolsState.GameActive);
            else
                StateManager.HandleFGTState(ToolsState.FGCGameActive);

            StateManager.HandleFGState(PlayerState.Active);

            FGBehaviour.FGCC.RigidBody.isKinematic = false;

            if (Spawnpoint == null)
            {
                if (StateManager.CheckpointModel != null)
                    Spawnpoint = GameObject.Instantiate(StateManager.CheckpointModel);
                else
                    Spawnpoint = GameObject.CreatePrimitive(PrimitiveType.Cube);

                Spawnpoint.DestroyComponentImmediateIfExists<BoxCollider>();
                Spawnpoint.GetComponent<MeshRenderer>().enabled = !InvisibleCheckpoint.Value;
                Spawnpoint.name = "Checkpoint";
                Spawnpoint.transform.SetPositionAndRotation(FGBehaviour.transform.position, FGBehaviour.transform.rotation);
                Spawnpoint.SetActive(true);

                if (SpeedrunMode.Value && !FGTServiceManager.GetService<SpeedrunService>().IsSepeedrunsDisabled)
                    FGTServiceManager.GetService<SpeedrunService>().PrepareForGameplay();
            }

            if (StateManager.FGTCurrentState != FGTStateManager.ToolsState.FGCGameActive)
            {
                //foreach (ScoreZoneManager zoneManager in Resources.FindObjectsOfTypeAll<ScoreZoneManager>())
                //    zoneManager.ActivateInitialZones();
                //foreach (PixelPerfectManager pixelManager in Resources.FindObjectsOfTypeAll<PixelPerfectManager>())
                //{
                //    pixelManager.Init();
                //    pixelManager.BeginGame();
                //}
            }

            if (SpeedrunMode.Value && QualLevel.Value == QualType.None)
            {
                DoModal(new(LocalizedStr("sp_qual_disabled_title"), LocalizedStr("sp_qual_disabled_desc"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, onClick: new Action<bool>(wasok => {
                    if (wasok)
                        QualLevel.Value = QualType.LoadRandomRoundAfter;
                })));
            }

            string currVer = FGToolsBuildDetails.Version;

            var oS = FGTServiceManager.GetService<OnlineCheckService>();

            if (FGTTargetSettings.UpdateNotification && oS.FGTContent.Config.OutdatedVersions != null && oS.FGTContent.Config.OutdatedVersions.Contains(currVer))
                CreateNotification(LocalizedStr("msg_outdated_ver_title"), LocalizedStr("msg_outdated_ver_short", [oS.FGTContent.Meta.FgtVersion]), FGT_Warning_Color);

            FGTServiceManager.GetService<StatisticsService>().CurrentStats.TotalRoundsLoaded++;
            FGBehaviour.OnGameplayBegin();

            FGTLog(LogLevel.Info, "OnIntroCountdownEnded", "Gameplay begins...");
        }

        public override void OnStateExit()
        {
            if (Spawnpoint != null) GameObject.DestroyImmediate(Spawnpoint);
            GameActions.OnRoundStarts -= OnGameplayBegins;
            GameActions.OnCheckpointReached -= OnCheckpoint;
        }

        public void RoundIntroGUI()
        {
            if (!StateManager.InternalState.LoaderUIToggle || !LocalServerService.IsUserAloneAndHost || !StateManager.IsIntroPlaying)
                return;

            var label = $"{LocalizedStr("intro_skip_msg", [SkipIntroHotkey.Value, SkipIntroTime.Value])}";
            if (holdingSkipIntro)
                label = $"{skipIntroHold:F1} / {SkipIntroTime.Value}";

            var disp = $"<b>{label}</b>".ToUpper();

            var lbSize = GUI.skin.label.CalcSize(new GUIContent(disp));
            var boxWidth = lbSize.x + 20f;

            GUI.Box(new Rect(Screen.width - boxWidth + 5f, 0f, boxWidth, 30f), "");
            GUI.Label(new Rect(Screen.width - boxWidth + 10f, 5f, lbSize.x, lbSize.y), disp);
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
        }

        public override void UpdateState()
        {
            if (!StateManager.IsIntroPlaying || !LocalServerService.IsUserAloneAndHost)
                return;

            if (Input.GetKey(SkipIntroHotkey.Value))
            {
                skipIntroHold += Time.deltaTime;

                if (!holdingSkipIntro)
                {
                    holdingSkipIntro = true;
                    skipIntroHold = 0f;
                }

                if (Input.GetKeyUp(SkipIntroHotkey.Value))
                {
                    holdingSkipIntro = false;
                    skipIntroHold = 0f;
                }

                else if (skipIntroHold >= SkipIntroTime.Value)
                {
                    CGM.SetReady(PlayerReadinessState.ReadyToPlay);
                    StateManager.HandleFGTState(ToolsState.IntroComplete);
                }
            }
            else
            {
                holdingSkipIntro = false;
                skipIntroHold = 0f;
            }
        }

        public override void DisplayGUI()
        {
            RoundIntroGUI();
        }

        internal void ResetRandomCosmetics()
        {
            var sect = GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections;

            FGBehaviour.FGCC.CustomisationHandler.UpdateCostumeOption(sect.CostumeTopOption, false);
            FGBehaviour.FGCC.CustomisationHandler.UpdateCostumeOption(sect.CostumeBottomOption, false);
            FGBehaviour.FGCC.CustomisationHandler.UpdateColourOption(sect.ColourOption);
            FGBehaviour.FGCC.CustomisationHandler.UpdateFaceplateColours(sect.FaceplateOption);
            FGBehaviour.FGCC.CustomisationHandler.UpdatePatternTexture(sect.PatternOption);

            var emotes = new Il2CppReferenceArray<EmotesOption>(8);
            var tempList1 = new Il2CppSystem.Collections.Generic.List<ItemDefinitionSO>(8);
            var tempList2 = new Il2CppSystem.Collections.Generic.List<ItemDefinitionSO>(8);
            int globalIndex = 0;

            for (int i = 0; i < sect.FirstWheelOptions.Count; i++)
            {
                var itm = sect.FirstWheelOptions[i];
                if (itm.CMSGroupID == "cosmetics_emotes")
                    emotes[globalIndex] = itm.Cast<EmotesOption>();

                tempList1.Add(itm);
                globalIndex++;
            }

            for (int i = 0; i < sect.SecondWheelOptions.Count; i++)
            {
                var itm = sect.SecondWheelOptions[i];
                if (itm.CMSGroupID == "cosmetics_emotes")
                    emotes[globalIndex] = itm.Cast<EmotesOption>();

                tempList2.Add(itm);
                globalIndex++;
            }

            XRayUtils.RemoveXRayControllerForCharacter(FGBehaviour.FGCC);

            FGBehaviour.FGCC.SetSocialOptions(sect.FirstWheelOptions, sect.SecondWheelOptions, emotes);

            if (PrimeHandler != null)
            {
                PrimeHandler.HighlightedSocialWheel.SocialItemsDictionary[WheelType.Phrases] = tempList1;
                PrimeHandler.HighlightedSocialWheel.SocialItemsDictionary[WheelType.EmotesAndEmoticons] = tempList2;
            }
        }

        internal void HandleRandomCosmetics()
        {
            if (!StateManager.IsInGameplay)
                return;

            var cms = CMSLoader.Instance;
            List<string> topIds = [.. cms._costumesUpperSO.CostumesTop.Keys];
            List<string> bottomIds = [.. cms._costumesLowerSO.CostumesBottom.Keys];
            List<string> patternIds = [.. cms._costumesPatternsSO.Patterns.Keys];
            List<string> colorsIds = [.. cms._costumesColourSchemasSO.Colours.Keys];
            List<string> facesIds = [.. cms._costumesFaceplatesSO.Faceplates.Keys];

            FGBehaviour.FGCC.CustomisationHandler.UpdateCostumeOption(CustomisationManager.Instance.GetUpperCostumeWithId(topIds[Random.RandomRange(0, topIds.Count)], true), false);
            FGBehaviour.FGCC.CustomisationHandler.UpdateCostumeOption(CustomisationManager.Instance.GetLowerCostumeWithId(bottomIds[Random.RandomRange(0, bottomIds.Count)], true), false);
            FGBehaviour.FGCC.CustomisationHandler.UpdateColourOption(CustomisationManager.Instance.GetColourOptionWithId(colorsIds[Random.RandomRange(0, colorsIds.Count)], true));
            FGBehaviour.FGCC.CustomisationHandler.UpdateFaceplateColours(CustomisationManager.Instance.GetFaceplateOptionWithId(facesIds[Random.RandomRange(0, facesIds.Count)], true));
            FGBehaviour.FGCC.CustomisationHandler.UpdatePatternTexture(CustomisationManager.Instance.GetSkinPatternOptionWithId(patternIds[Random.RandomRange(0, patternIds.Count)], true));

            var sect = GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections;

            int globalIndex = 0;
            var tempRes1 = new Il2CppReferenceArray<ItemDefinitionSO>(8);
            var tempRes2 = new Il2CppReferenceArray<ItemDefinitionSO>(8);
            var emoteOption = new Il2CppReferenceArray<EmotesOption>(8);

            SetRandomWheel(sect.FirstWheelOptions, ref globalIndex, ref emoteOption, ref tempRes1, out var tempRes1List);
            SetRandomWheel(sect.SecondWheelOptions, ref globalIndex, ref emoteOption, ref tempRes2, out var tempRes2List);

            XRayUtils.RemoveXRayControllerForCharacter(FGBehaviour.FGCC);

            FGBehaviour.FGCC.SetSocialOptions(tempRes1, tempRes2, emoteOption);

            if (PrimeHandler != null)
            {
                PrimeHandler.HighlightedSocialWheel.SocialItemsDictionary[WheelType.Phrases] = tempRes1List;
                PrimeHandler.HighlightedSocialWheel.SocialItemsDictionary[WheelType.EmotesAndEmoticons] = tempRes2List;
            }
        }

        static void SetRandomWheel(Il2CppReferenceArray<ItemDefinitionSO> wheel, ref int globalIndex, ref Il2CppReferenceArray<EmotesOption> emotes, ref Il2CppReferenceArray<ItemDefinitionSO> array, out Il2CppSystem.Collections.Generic.List<ItemDefinitionSO> list)
        {
            list = new(8);

            var cms = CMSLoader.Instance;
            List<string> emotesIds = [.. cms._cosmeticsEmoteSO.Emotes.Keys];
            List<string> emoticonIds = [.. cms._cosmeticsEmoticonsSO.Emoticons.Keys];
            List<string> phraseIds = [.. cms._cosmeticsPhrasesSO.Phrases.Keys];

            for (int i = 0; i < wheel.Count; i++)
            {
                var opt = wheel[i];

                if (opt.CMSGroupID == "cosmetics_emotes")
                {
                    var emt = CustomisationManager.Instance.GetEmoteOptionWithId(emotesIds[Random.RandomRange(0, emotesIds.Count)], true);
                    emotes[globalIndex] = emt;
                    array[i] = emt;
                }

                if (opt.CMSGroupID == "cosmetics_phrases")
                {
                    var phrase = CustomisationManager.Instance.GetPhraseOptionWithId(phraseIds[Random.Range(0, phraseIds.Count)], true);
                    array[i] = phrase;
                }

                if (opt.CMSGroupID == "cosmetics_emoticons")
                {
                    var emoticon = CustomisationManager.Instance.GetEmoticonOptionWithId(emoticonIds[Random.Range(0, emoticonIds.Count)], true);
                    array[i] = emoticon;
                }

                list.Add(array[i]);

                globalIndex++;
            }
        }
    }
}
