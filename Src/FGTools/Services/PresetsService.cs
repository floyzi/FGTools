using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Events;
using FG.Common;
using FG.Common.Definition;
using FGClient;
using FGClient.Rendering.XRay;
using FGClient.UI;
using FGTools.Config;
using FGTools.Internal.Extensions;
using FGTools.Services.Logic;
using FGTools.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniverseLib.UI;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using Text = UnityEngine.UI.Text;
namespace FGTools.Services
{
    internal class PresetsService : FGTService, IFGTGUIHelper
    {
        public class PresetBase
        {
            public string Name { get; set; }
            public DateTime CreationDate { get; set; }
            public PresetJson PresetContent { get; set; }
        }

        public class PresetJson
        {
            public string Color { get; set; }
            public string Pattern { get; set; }
            public string Faceplate { get; set; }
            public string Upper { get; set; }
            public string Lower { get; set; }
            public string Victory { get; set; }
            public string Nameplate { get; set; }
            public string Nickname { get; set; }
            public List<string> FirstWheel {  get; set; }
            public List<string> SecondWheel { get; set; }
        }

        public Dictionary<string, Dictionary<string, PresetBase>> Presets;
        EventSystem.Handle OnMenu;
        bool _ignoreMenu;
        string SelectedPresetPath = "...";
        string SelectedPresetName;
        Dropdown PresetsDrop;
        Text presetInfo;

        public void SetDataForPreset(string name)
        {
            var loadout = GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections;
            if (loadout != null)
            {
                List<string> firstWheel = [];
                List<string> secondWheel = new();

                foreach (var item in loadout.FirstWheelOptions)
                {
                    firstWheel.Add(item.name);
                }

                foreach (var item in loadout.SecondWheelOptions)
                {
                    secondWheel.Add(item.name);
                }

                var newPreset = new PresetBase()
                {
                    Name = name,
                    CreationDate = DateTime.Now,
                    PresetContent = new()
                    {
                        Color = loadout.ColourOption.name,
                        Pattern = loadout.PatternOption.name,
                        Faceplate = loadout.FaceplateOption.name,
                        Lower = loadout.CostumeBottomOption.name,
                        Upper = loadout.CostumeTopOption.name,
                        Victory = loadout.VictoryPoseOption.name,
                        Nameplate = loadout.NameplateOption.name,
                        Nickname = loadout.NicknameOption.ItemId,   
                        FirstWheel = firstWheel,
                        SecondWheel = secondWheel,
                    }
                };
                var path = Path.Combine(Launcher.PresetsDir, $"FGTPreset_{name}.json");
                File.WriteAllText(path, JsonSerializer.Serialize(newPreset));
                if (Presets.ContainsKey(name))
                    Presets.Remove(name);

                Presets.Add(newPreset.Name, new() { { path, newPreset } });
            }
        }

        public string PreviewPresetContent(string name)
        {
            var preset = PresetFromPath(Path.Combine(Launcher.PresetsDir, name));
            if (preset != null)
            {
                int wheelIndex = 1;
                int[] idx = new int[3];
                for (int i = 0; i < idx.Length; i++)
                {
                    idx[i] = 1;
                }

                StringBuilder output = new StringBuilder();
                output.AppendLine($"<b>{LocalizedStr("gui_preset_info")} | {LocalizedStr("gui_preset_date")}: {preset.CreationDate}</b>\n".ToUpper());
                output.AppendLine($"- {LocalizedStr("gui_preset_color")}: <color=grey><i>{preset.PresetContent.Color}</i></color> - <i>{GetItemName(preset.PresetContent.Color)}</i>");
                output.AppendLine($"- {LocalizedStr("gui_preset_pattern")}: <color=grey><i>{preset.PresetContent.Pattern}</i></color> - <i>{GetItemName(preset.PresetContent.Pattern)}</i>");
                output.AppendLine($"- {LocalizedStr("gui_preset_faceplate")}: <color=grey><i>{preset.PresetContent.Faceplate}</i></color> - <i>{GetItemName(preset.PresetContent.Faceplate)}</i>");
                output.AppendLine($"- {LocalizedStr("gui_preset_upper")}: <color=grey><i>{preset.PresetContent.Upper}</i></color> - <i>{GetItemName(preset.PresetContent.Upper)}</i>");
                output.AppendLine($"- {LocalizedStr("gui_preset_bottom")}: <color=grey><i>{preset.PresetContent.Lower}</i></color> - <i>{GetItemName(preset.PresetContent.Lower)}</i>");
                output.AppendLine($"- {LocalizedStr("gui_preset_nickname")}: <color=grey><i>{preset.PresetContent.Nickname}</i></color> - <i>{GetItemName(preset.PresetContent.Nickname)}</i>");
                output.AppendLine($"- {LocalizedStr("gui_preset_nameplate")}: <color=grey><i>{preset.PresetContent.Nameplate}</i></color> - <i>{GetItemName(preset.PresetContent.Nameplate)}</i>");
                output.AppendLine($"- {LocalizedStr("gui_preset_win_anim")}: <color=grey><i>{preset.PresetContent.Victory}</i></color> - <i>{GetItemName(preset.PresetContent.Victory)}</i>");
                output.AppendLine($"");

                foreach (var wheel in new[] { preset.PresetContent.FirstWheel, preset.PresetContent.SecondWheel })
                {
                    foreach (var str in wheel)
                    {
                        string type = "";
                        int index = -1;

                        if (str.Contains("Emote"))
                        {
                            type = "gui_preset_emotes";
                            index = idx[0]++;
                        }
                        else if (str.Contains("Emoji"))
                        {
                            type = "gui_preset_emoticon";
                            index = idx[1]++;
                        }
                        else if (str.Contains("Phrase"))
                        {
                            type = "gui_preset_phrase";
                            index = idx[2]++;
                        }

                        output.AppendLine($"- {LocalizedStr(wheel == preset.PresetContent.FirstWheel ? "gui_preset_first_wheel" : "gui_preset_second_wheel")} {wheelIndex} ({LocalizedStr(type)} {index}): <color=grey><i>{str}</i></color> - <i>{GetItemName(str)}</i>");
                        wheelIndex++;
                    }
                    wheelIndex = 1;
                    output.AppendLine($"");
                }

                return output.ToString();
            }
            return string.Empty;
        }


        public void SetPreset(string name, bool ignoreMenu, bool silent = false)
        {
            var preset = PresetFromPath(Path.Combine(Launcher.PresetsDir, name));
            if (preset == null)
                return;

            var a = Resources.FindObjectsOfTypeAll<NicknamesSO>().FirstOrDefault()?.Nicknames;
            var b = Resources.FindObjectsOfTypeAll<CosmeticsEmoticonsSO>().FirstOrDefault()?.Emoticons;
            var c = Resources.FindObjectsOfTypeAll<CosmeticsPhrasesSO>().FirstOrDefault()?.Phrases;

            var selections = GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections;

            selections.ColourOption = GetItem<ColourOption>(preset.PresetContent.Color);
            selections.PatternOption = GetItem<SkinPatternOption>(preset.PresetContent.Pattern);
            selections.FaceplateOption = GetItem<FaceplateOption>(preset.PresetContent.Faceplate);
            selections.CostumeTopOption = GetItem<CostumeOption>(preset.PresetContent.Upper);
            selections.CostumeBottomOption = GetItem<CostumeOption>(preset.PresetContent.Lower);
            selections.VictoryPoseOption = GetItem<VictoryOption>(preset.PresetContent.Victory);
            selections.NameplateOption = GetItem<NameplateOption>(preset.PresetContent.Nameplate);

            NicknameOption nicknameOption = new();
            nicknameOption.SetCMSData(a?[preset.PresetContent.Nickname]);
            selections.NicknameOption = nicknameOption;

            int i = 0;

            foreach (var item in preset.PresetContent.FirstWheel)
            {
                if (item.Contains("Emote"))
                    selections.FirstWheelOptions[i] = GetItem<EmotesOption>(item);
                else if (item.Contains("Emoji"))
                    selections.FirstWheelOptions[i] = GetItem<ImageSpeechOption>(item);
                else if (item.Contains("Phrase"))
                    selections.FirstWheelOptions[i] = GetItem<TextAndImageSpeechOption>(item);

                i++;
            }

            i = 0;

            foreach (var item in preset.PresetContent.SecondWheel)
            {
                if (item.Contains("Emote"))
                    selections.SecondWheelOptions[i] = GetItem<EmotesOption>(item);
                else if (item.Contains("Emoji"))
                    selections.SecondWheelOptions[i] = GetItem<ImageSpeechOption>(item);
                else if (item.Contains("Phrase"))
                    selections.SecondWheelOptions[i] = GetItem<TextAndImageSpeechOption>(item);

                i++;
            }

            FGTServiceManager.GetService<EventService>().SetEventValue("LatestUserPreset", name);

            if (!StateManager.IsInGameplay && !StateManager.IsInEditor)
            {
                var mmm = Resources.FindObjectsOfTypeAll<MainMenuManager>().FirstOrDefault();
                mmm?.ApplyOutfit();
                _ignoreMenu = ignoreMenu;
                if (!silent)
                    mmm?.ShowMainMenu(false, true, false);
            }
            else
            {
                FallguyCustomisationHandler handler = null;

                if (StateManager.IsInGameplay)
                    handler = FGBehaviour.GetComponent<FallguyCustomisationHandler>();
                //else if (StateManager.IsInEditor)
                //    handler = Resources.FindObjectsOfTypeAll<FallguyCustomisationHandler>().FirstOrDefault();


                if (handler == null)
                    return;

                handler.UpdateColourOption(selections.ColourOption);
                handler.UpdateCostumeOption(selections.CostumeTopOption, false);
                handler.UpdateCostumeOption(selections.CostumeBottomOption, false);
                handler.UpdateFaceplateColours(selections.FaceplateOption);
                handler.UpdatePatternTexture(selections.PatternOption);

                CustomisationManager.Instance.ApplyCustomisationsToFallGuy(handler.gameObject, selections, -1);
                XRayUtils.RemoveXRayControllerForCharacter(handler.gameObject.GetComponent<FallGuysCharacterController>());
            }
        }

        void OnMenuEnter(OnMainMenuDisplayed evt)
        {
            if (!_ignoreMenu && Config.Config.AutoSetPreset.Value)
            {
                string value = FGTServiceManager.GetService<EventService>().ReturnStringEventValue("LatestUserPreset");

                if (string.IsNullOrEmpty(value))
                    return;

                var preset = PresetFromPath(value);
                if (preset == null)
                    return;

                SetPreset(value, true, true);
                CreateNotification(LocalizedStr("latest_preset_set_title"), LocalizedStr("latest_preset_set_desc", [preset.Name]), FGT_Info_Color, 5);
            }
        }

        PresetBase PresetFromPath(string path)
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<PresetBase>(File.ReadAllText(path));

            return null;
        }

        public override void RegisterService()
        {
            Presets = [];
            foreach (var file in Directory.GetFiles(Launcher.PresetsDir))
            {
                try
                {
                    var presetBase = JsonSerializer.Deserialize<PresetBase>(File.ReadAllText(file));
                    Presets.Add(presetBase.Name, new() { { file, presetBase } });
                }
                catch { }
            }

            FGTLog(BepInEx.Logging.LogLevel.Info, base.GetType(), $"Found \"{Presets.Count}\" presets.");
            OnMenu = Broadcaster.Instance.Register<OnMainMenuDisplayed>(new Action<OnMainMenuDisplayed>(OnMenuEnter));
        }

        public override void DrawGUI()
        {
        }

        public void SetUIReferences(object[] data)
        {
            PresetsDrop = (Dropdown)data[0];
            presetInfo = (Text)data[1];

            InitPresetsDrop();
        }

        void InitPresetsDrop()
        {
            PresetsDrop.ClearOptions();

            Il2CppSystem.Collections.Generic.List<string> a = new();
            a.Add(LocalizedStr("dropdown_placeholder"));
            foreach (string preset in Presets.Keys)
            {
                if (!a.Contains(preset))
                    a.Add(preset);
            }

            PresetsDrop.AddOptions(a);
            PresetsDrop.Hide();
        }

        public void OnPresetsDropSelect(int index)
        {
            if (index == 0)
            {
                presetInfo.text = LocalizedStr("gui_presets_desc");
                return;
            }

            SelectedPresetPath = Presets[PresetsDrop.options[index].text].Keys.First();
            presetInfo.text = PreviewPresetContent(SelectedPresetPath);
            SelectedPresetName = PresetsDrop.options[index].text;
        }

        public void TryDeletePreset()
        {
            if (SelectedPresetPath != "default")
            {
                ReadyPopups.AreYouSurePopup($"{LocalizedStr("gui_preset_delete_act", [SelectedPresetName])}", popAct: new Action<bool>((bool wasOk) =>
                {
                    if (wasOk)
                    {
                        File.Delete(Path.Combine(SelectedPresetPath));
                        if (Presets.ContainsKey(SelectedPresetName))
                            Presets.Remove(SelectedPresetName);

                        InitPresetsDrop();
                    }
                }));
            }
        }
        
        public void TryUsePreset()
        {
            if (SelectedPresetPath != "default")
                SetPreset(SelectedPresetPath, true);
            else
                ReadyPopups.ErrorPopup(LocalizedStr("gui_default_load"));
        }

        public void MakeNewPresetPopup()
        {
            AddCMSString("preset_holder", $"{LocalizedStr("preset_holder")}");
            UniversalUI.SetUIActive(UniverseGUID, false); FGToolsUI.NewGUI.Instance.UIRoot.gameObject.SetActive(false); StateManager.InternalState.LoaderUIToggle = false;
            string presetName = "";

            var ModalMessageDataDisclaimer = new ModalMessageWithInputFieldData
            {
                Title = LocalizedStr("newpreset_title"),
                Message = LocalizedStr("newpreset_desc"),
                LocaliseMessage = UIModalMessage.LocaliseOption.NotLocalised,
                LocaliseTitle = UIModalMessage.LocaliseOption.NotLocalised,
                ModalType = UIModalMessage.ModalType.MT_OK_CANCEL,
                OkButtonType = UIModalMessage.OKButtonType.Default,
                OnCloseButtonPressed = new Action<bool>(wasOk =>
                {
                    if (wasOk)
                    {
                        SetDataForPreset(presetName);
                    }

                    InitPresetsDrop();
                    UniversalUI.SetUIActive(UniverseGUID, true);
                    FGToolsUI.NewGUI.Instance.UIRoot.gameObject.SetActive(true);
                    StateManager.InternalState.LoaderUIToggle = true;
                }),
                InputTextPlaceholder = "preset_holder",
            };

            PopupManager.Instance.Show(PopupInteractionType.Error, ModalMessageDataDisclaimer);
            AudioManager.PlayOneShot(AudioManager.EventMasterData.GenericPopUpAppears);
            GameObject inputfiledig = GameObject.Find("InputField");
            inputfiledig.GetComponent<TMP_InputField>().onValueChanged.AddListener(new Action<string>(val => { presetName = val; }));
        }

        public override void UpdateService()
        {
            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                _ignoreMenu = false;     
            }
        }

        public void RefreshUI()
        {
            InitPresetsDrop();
        }
    }
}
