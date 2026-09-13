extern alias wle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Events;
using FG.Common;
using FG.Common.Fraggle;
using FG.Common.UGCNetworking;
using FGClient;
using FGClient.UI;
using FGTools.Config;
using FGTools.Content;
using FGTools.Internal.Extensions;
using FGTools.Services.Logic;
using FGTools.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using wle::FG.Common.LevelEditor.Serialization;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;

namespace FGTools.Services
{
    internal class FGC_LocalSavesService : FGTService, IFGTGUIHelper
    {
        static readonly byte[] XorKey = Encoding.UTF8.GetBytes("R8byo6?Uxv№Uokh$B;wfVБvo8>LP<Or4");
        const string DatePattern = "HH_mm_ss@dd_M_yyyy";
        const string lvlName = "level.json";
        const string dtoName = "dto.json";
        const string picName = "preview.png";
        public List<SaveMetadata> SavedMetadata;
        List<SaveMetadata> MetadatasOfLevel = new();
        public SaveMetadata SelectedAutosave;
        string SavePath;
        string LevelPath;
        string SelectedLevelName;
        Dropdown[] dropdowns;
        Image SaveImg;
        Text SaveTextObj;
        GameObject SaveInfoDisplay;
        GameObject LoadBtn;
        Button NewSaveBtn;
        GameObject ActionsDisplay;
        Action<LevelEditorEnterEvent> LevelEditorEnter;
        Action<LevelEditorExitEvent> LevelEditorExit;
        Text NoSaves;
        GameObject DropdownsGroup;
        GameObject SaveManageGroup;
        Button DeleteSave;
        Button DeleteLevel;

        public class SaveMetadata
        {
            public string SaveName { get; set; }
            public string LevelName { get; set; }
            public string LevelChecksum { get; set; }
            public string DTOChecksum { get; set; }
            public DateTime SaveTime { get; set; }
        }

        void LevelEditorEnterEvt(LevelEditorEnterEvent e)
        {
            NewSaveBtn.gameObject.SetActive(true);
        }

        void LevelEditorExitEvt(LevelEditorExitEvent e)
        {
            NewSaveBtn.gameObject.SetActive(false);
        }

        public override void DrawGUI()
        {

        }

        public override void RegisterService()
        {
            if (!Directory.Exists(Launcher.FGCAutosavesDir))
                Directory.CreateDirectory(Launcher.FGCAutosavesDir);

            LevelEditorEnter = new Action<LevelEditorEnterEvent>(LevelEditorEnterEvt);
            LevelEditorExit = new Action<LevelEditorExitEvent>(LevelEditorExitEvt);
            Broadcaster.Instance.Register<LevelEditorEnterEvent>(LevelEditorEnter);
            Broadcaster.Instance.Register<LevelEditorExitEvent>(LevelEditorExit);
            LoadMetadata();
        }

        string CalculateChecksum(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    string checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    //byte[] bytes = Encoding.UTF8.GetBytes(checksum);
                    //FGTInternalTools.XorByteArray(ref bytes, XorKey);
                    //return Encoding.UTF8.GetString(bytes);
                    return checksum;
                }
            }
        }

        public void SetUIReferences(object[] data)
        {
            dropdowns = new Dropdown[2];
            dropdowns[0] = (Dropdown)data[0];
            dropdowns[1] = (Dropdown)data[1];
            SaveImg = (Image)data[2];
            SaveTextObj = (Text)data[3];
            SaveInfoDisplay = (GameObject)data[4];
            LoadBtn = (GameObject)data[5];
            ActionsDisplay = (GameObject)data[6];
            NewSaveBtn = (Button)data[7];
            NoSaves = (Text)data[8];
            DropdownsGroup = (GameObject)data[9];
            SaveManageGroup = (GameObject)data[10];
            DeleteLevel = (Button)data[11];
            DeleteSave = (Button)data[12];

            RefreshLevelsDropdown();
        }


        public void TryDeleteLevel()
        {
            if (SelectedAutosave == null)
                return;

            string modalMessage = $"{LocalizedStr("gui_deletion_generic_warning")}\n{LocalizedStr("gui_local_save_del_level_desc", [SelectedAutosave.LevelName, SavedMetadata.FindAll(x => x.LevelName == SelectedAutosave.LevelName).Count.ToString()])}\n\n{LocalizedStr("gui_space_after_deletion", [CalculateSizeString(CalculateDirSize(LevelPath))])}";
            DoModal(new(LocalizedStr("gui_local_save_del_level_title", [SelectedAutosave.LevelName]), modalMessage, UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Disruptive, new Action<bool>(val => 
            {
                if (val)
                {
                    Directory.Delete(LevelPath, true);
                    for (int i = SavedMetadata.Count - 1; i >= 0; i--)
                    {
                        if (SavedMetadata[i].LevelName == SelectedLevelName)
                            SavedMetadata.RemoveAt(i);
                    }

                    WriteSave();
                    RefreshLevelsDropdown();
                }
            }), hideLvl: ModalHideGUIType.KeepHiddenForThisModal));
        }

        public void TryDeleteSave()
        {
            if (SelectedAutosave == null)
                return;

            string modalMessage = $"{LocalizedStr("gui_deletion_generic_warning")}\n{LocalizedStr("gui_local_save_del_save_desc", [SelectedAutosave.SaveName, SelectedAutosave.LevelName])}\n\n{LocalizedStr("gui_space_after_deletion", [CalculateSizeString(CalculateDirSize(Path.Combine(SavePath)))])}";
            DoModal(new(LocalizedStr("gui_local_save_del_save_title", [SelectedAutosave.SaveName]), modalMessage, UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Disruptive, new Action<bool>(val =>
            {
                if (val)
                {
                    Directory.Delete(SavePath, true);
                    SavedMetadata.Remove(SelectedAutosave);
                    WriteSave();
                    RefreshLevelsDropdown();
                }
            }), hideLvl: ModalHideGUIType.KeepHiddenForThisModal));
        }

        public void TryDeleteEverything()
        {
            if (MetadatasOfLevel.Count == 0)
            {
                FLZ_Extensions.CreateNotification(LocalizedStr("gui_local_save_no_levels_title"), LocalizedStr("gui_local_save_no_levels_desc"));
                return;
            }

            string modalMessage = $"{LocalizedStr("gui_deletion_generic_warning")}\n{LocalizedStr("gui_delete_local_saves_desc")}\n\n{LocalizedStr("gui_space_after_deletion", [CalculateSizeString(CalculateDirSize(Launcher.FGCAutosavesDir))])}";
            DoModal(new(LocalizedStr("gui_delete_local_saves_title"), modalMessage, UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Disruptive, new Action<bool>(val =>
            {
                if (val)
                {
                    Directory.Delete(Launcher.FGCAutosavesDir, true);
                    Application.Quit();
                }
            }), hideLvl: ModalHideGUIType.KeepHiddenForThisModal));
        }

        void RefreshLevelsDropdown()
        {
            var holder = new Il2CppSystem.Collections.Generic.List<string>();
            holder.Add(LocalizedStr("dropdown_placeholder"));

            dropdowns[0].Hide();
            dropdowns[0].ClearOptions();
            dropdowns[1].ClearOptions();

            Il2CppSystem.Collections.Generic.List<string> knownLevels = new Il2CppSystem.Collections.Generic.List<string>();
            foreach (var save in SavedMetadata)
            {
                var name = save.LevelName;
                if (!knownLevels.Contains(name))
                    knownLevels.Add(name);
            }

            LoadBtn.gameObject.SetActive(knownLevels.Count > 0);
            DropdownsGroup.gameObject.SetActive(knownLevels.Count > 0);
            NewSaveBtn.gameObject.SetActive(FraggleCommonManager.Instance.IsInLevelEditor);
            NoSaves.gameObject.SetActive(knownLevels.Count == 0);

            dropdowns[0].AddOptions(holder);
            dropdowns[0].AddOptions(knownLevels);
            dropdowns[1].AddOptions(holder);

            SaveInfoDisplay.gameObject.SetActive(false);
            SaveManageGroup.gameObject.SetActive(false);
            LoadBtn.gameObject.SetActive(false);
        }

        bool MeetDefaultRequirnments()
        {
            return StateManager.IsInEditor && wle.FG.Common.LevelEditorManagerProxy.CurrentLevel != null;
        }

        public void TryToLoadSelectedSave()
        {
            if (SelectedAutosave == null)
            {
                DoModal(new(LocalizedStr("gui_local_save_metadata_error_title"), LocalizedStr("gui_local_save_metadata_error_desc"), FGClient.UI.UIModalMessage.ModalType.MT_OK, FGClient.UI.UIModalMessage.OKButtonType.Disruptive));
                return;
            }

            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                DoModal(new(LocalizedStr("gui_local_save_menu_error_title"), LocalizedStr("gui_local_save_menu_error_desc"), FGClient.UI.UIModalMessage.ModalType.MT_OK, FGClient.UI.UIModalMessage.OKButtonType.Default));
                return;
            }    

            if (LoadSave(Path.Combine(Launcher.FGCAutosavesDir, SelectedAutosave.LevelName, SelectedAutosave.SaveName)))
            {
                var mmm = Resources.FindObjectsOfTypeAll<MainMenuManager>().FirstOrDefault();
                if (mmm == null)
                    return;

                mmm.RemoveMainMenuBuilder();
                mmm.StopMusic();
            }
        }

        bool IsChecksumValid(string storedChecksum, string newChecksum)
        {
            return storedChecksum == newChecksum;
        }

        bool LoadSave(string pathBase)
        {
            var lvl = Path.Combine(pathBase, lvlName);
            var dto = Path.Combine(pathBase, dtoName);

            if (!File.Exists(lvl) || !File.Exists(dto))
            {
                DoModal(new(LocalizedStr("gui_local_save_missing_error_title"), LocalizedStr("gui_local_save_missing_error_desc"), FGClient.UI.UIModalMessage.ModalType.MT_OK, FGClient.UI.UIModalMessage.OKButtonType.Disruptive));
                return false;
            }

            var proxy = wle.FG.Common.LevelEditorManagerProxy.Instance;

            try
            {
                var metadata = SavedMetadata.Find(x => x.SaveName == SelectedAutosave.SaveName);
                if (!IsChecksumValid(metadata.LevelChecksum, CalculateChecksum(lvl)) || !IsChecksumValid(metadata.DTOChecksum, CalculateChecksum(dto)))
                {
                    FGTLog(BepInEx.Logging.LogLevel.Info, base.GetType(), "Autosave failed checksum check!");
                    DoModal(new(LocalizedStr("gui_local_save_checksum_fail_title"), LocalizedStr("gui_local_save_checksum_fail_desc"), FGClient.UI.UIModalMessage.ModalType.MT_OK, FGClient.UI.UIModalMessage.OKButtonType.Disruptive));
                    return false;
                }

                FGTLog(BepInEx.Logging.LogLevel.Info, base.GetType(), "Autosave passed checksum check!");

                var options = LevelEditorOptionsSingleton.Instance;
                var commonManager = FraggleCommonManager.Instance;
                var json = File.ReadAllText(lvl);

                CreativeModeJSONObject<LevelAggregateDto>.DeserializeObject(File.ReadAllText(dto), out LevelAggregateDto lvlDto);

                wle.FG.Common.LevelEditorManagerProxy.LevelEditorLevels.ResetCurrentLevelAndClearList();
                var level = wle.FG.Common.LevelEditorManagerProxy.LevelEditorLevels.Add(lvlDto, true);
                level ??= new wle.LevelEditorLevel(wle.FG.Common.LevelEditorManagerProxy.LevelEditorLevels, 0);

                options.StartMode = LevelEditorOptionsSingleton.StartModeType.Load;
                proxy.BlockInputForLoading = true;
                proxy.ShowLoadingScreen();
           
                var loader = wle.LevelLoader.CreateLevelLoaderFromDownloadedJSON(json, lvlDto);
                wle.FG.Common.LevelEditorManagerProxy.LevelEditorLevels.SetCurrentLevel(level);
                options.ClearGameModeRulebookValues();

                level._levelJSON = UGCNFetchedString.CreatePreFetchedResource(json);
                level.levelLoader = loader;
                commonManager.IsInLevelEditor = true;
                Broadcaster.Instance.RaiseEvent<LevelEditorEnterEvent>(new(null, LevelEditorElementType.Unknown));
                return true;
            }
            catch (Exception ex)
            {
                ReadyPopups.ErrorPopup(ex, title: "gui_local_save_generic_error_title", desc: "gui_local_save_generic_error_desc", displayOnlyError: false);
                proxy.BlockInputForLoading = false;
                proxy.HideLoadingScreen();
                return false;
            }

        }

        public void OnLevelSelect(int i)
        {
            MetadatasOfLevel.Clear();
            SelectedLevelName = dropdowns[0].options[i].text;

            dropdowns[1].Hide();
            dropdowns[1].ClearOptions();

            MetadatasOfLevel = SavedMetadata.FindAll(x => x.LevelName.Equals(SelectedLevelName));

            var stupidIl2cppList = new Il2CppSystem.Collections.Generic.List<string>();


            if (MetadatasOfLevel == null || MetadatasOfLevel.Count == 0)
            {
                DeleteLevel.gameObject.SetActive(false);
                SaveInfoDisplay.gameObject.SetActive(false);
                SaveManageGroup.gameObject.SetActive(false);
                stupidIl2cppList.Add(LocalizedStr("dropdown_placeholder"));
                dropdowns[1].AddOptions(stupidIl2cppList);
                LoadBtn.gameObject.SetActive(false);
                return;
            }

            LevelPath = Path.Combine(Launcher.FGCAutosavesDir, SelectedLevelName);

            foreach (var a in MetadatasOfLevel)
                stupidIl2cppList.Add(a.SaveName);

            dropdowns[1].AddOptions(stupidIl2cppList);

            SaveManageGroup.gameObject.SetActive(true);
            DeleteLevel.gameObject.SetActive(true);

            OnSaveSelect(0);
        }

        public void OnSaveSelect(int i)
        {
            var selectedLvl = dropdowns[1].options[i].text;
            SelectedAutosave = SavedMetadata.Find(x => x.SaveName.Equals(selectedLvl));

            if (SelectedAutosave == null)
            {
                SaveInfoDisplay.gameObject.SetActive(false);
                SaveManageGroup.gameObject.SetActive(false);
                DeleteSave.gameObject.SetActive(false);
                return;
            }

            var baseDir = Path.Combine(Launcher.FGCAutosavesDir, SelectedAutosave.LevelName, SelectedAutosave.SaveName);
            SavePath = baseDir;
            var pic = Path.Combine(baseDir, picName);
            var dto = Path.Combine(baseDir, dtoName);

            CreativeModeJSONObject<LevelAggregateDto>.DeserializeObject(File.ReadAllText(dto), out LevelAggregateDto lvlDto);

            SaveImg.gameObject.SetActive(File.Exists(pic));

            if (SaveImg.gameObject.activeSelf)
                SaveImg.sprite = GetSpriteFromFile(pic, 2048, 1024);

            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine($"{LocalizedStr("gui_local_save_stat_0")}: {SelectedAutosave.LevelName}");
            stringBuilder.AppendLine($"{LocalizedStr("gui_local_save_stat_1")}: {SelectedAutosave.SaveTime}");
            if (lvlDto != null)
            {
                stringBuilder.AppendLine($"\n{LocalizedStr("gui_local_save_stat_level")}\n");
                var lastVer = lvlDto.VersionHistory.Last();
                stringBuilder.AppendLine($"{LocalizedStr("gui_local_save_stat_2")}: {lastVer.Status} {(lastVer.Status == LevelVersionMetadataDto.EStatus.Fresh ? $"(<color=yellow>{LocalizedStr("gui_local_save_fresh_warn")}</color>)" : string.Empty)}");
                stringBuilder.AppendLine($"{LocalizedStr("gui_local_save_stat_3")}: {lastVer.Version}");
            }
            SaveTextObj.text = stringBuilder.ToString();

            DeleteSave.gameObject.SetActive(true);
            SaveInfoDisplay.gameObject.SetActive(true);
            LoadBtn.gameObject.SetActive(true);
        }

        public void TryAutosaveLevel()
        {
            try
            {
                if (FGTTargetSettings.FGCLocalSaves && (Config.Config.EnableLocalAutosaves.Value || MeetDefaultRequirnments()))
                    OnLevelAutosaved();
            }
            catch (Exception ex) 
            {

            }
        }

        void OnLevelAutosaved()
        {
            var saveDate = DateTime.Now;
            string saveDateStr = $"{saveDate.ToString(DatePattern)}";

            var manager = Resources.FindObjectsOfTypeAll<wle.FG.Common.LevelEditorManagerIO>().FirstOrDefault();

            if (manager == null)
            {
                FGTLog(BepInEx.Logging.LogLevel.Error, base.GetType(), "Tried to create local level save with null LevelEditorManagerIO");
                return;
            }

            var saveDir = Path.Combine(Launcher.FGCAutosavesDir, manager.LevelName);

            if (!Directory.Exists(saveDir))
                Directory.CreateDirectory(saveDir);

            string saveName = $"Autosave_{saveDateStr}-{Directory.GetDirectories(saveDir).Length + 1}";
            string picSaveName = $"{saveName}_PIC.png";
            var savePath = Path.Combine(saveDir, saveName);
            var picsPath = Path.Combine(saveDir, "Pics");
            var levelPath = Path.Combine(savePath, lvlName);
            var dtoPath = Path.Combine(savePath, dtoName);
            var pic = Path.Combine(savePath, picName);

            var saver = manager.InstantiateLeverSaver();
            var lvlThumb = wle.LevelSaver.CaptureThumbnail(false);
            var savedLevel = saver.PopulateSchema(LevelEditorOptionsSingleton.Instance);
            var json = UGCJsonSerializer.SerializeObject(savedLevel);
            var dtoJson = UGCJsonSerializer.SerializeObject(wle.FG.Common.LevelEditorManagerProxy.CurrentLevel.Dto);

            Directory.CreateDirectory(savePath);

            File.WriteAllText(levelPath, json);
            File.WriteAllText(dtoPath, dtoJson);
            File.WriteAllBytes(pic, lvlThumb);

            var newSaveMetadata = new SaveMetadata()
            {
                LevelChecksum = CalculateChecksum(levelPath),
                DTOChecksum = CalculateChecksum(dtoPath),
                LevelName = manager.LevelName,
                SaveName = saveName,
                SaveTime = saveDate,
            };

            SavedMetadata.Add(newSaveMetadata);

            WriteSave();

            RefreshLevelsDropdown();

            FGTLog(BepInEx.Logging.LogLevel.Info, base.GetType(), "Successfully created local level save");
        }

        public override void UpdateService()
        {

        }

        void LoadMetadata()
        {

            if (File.Exists(Launcher.FGCAutosavesMetadata))
            {
                try
                {
                    var bytes = File.ReadAllBytes(Launcher.FGCAutosavesMetadata);
                    XorByteArray(ref bytes, XorKey);

                    SavedMetadata = System.Text.Json.JsonSerializer.Deserialize<List<SaveMetadata>>(Encoding.UTF8.GetString(bytes));

                    FGTLog(BepInEx.Logging.LogLevel.Info, base.GetType(), "Metadata loaded.");
                }
                catch (Exception ex)
                {
                    SavedMetadata = new();

                    FGTLog(BepInEx.Logging.LogLevel.Error, base.GetType(), $"Unable to read metadata ({ex.Message}), new one was created.");
                }
            }
            else
            {
                SavedMetadata = new();

                FGTLog(BepInEx.Logging.LogLevel.Info, base.GetType(), "New metadata was created.");
            }

            WriteSave();
        }

        void WriteSave()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(SavedMetadata);
            var bytes = Encoding.UTF8.GetBytes(json);

            XorByteArray(ref bytes, XorKey);

            File.WriteAllBytes(Launcher.FGCAutosavesMetadata, bytes);
        }

        public void RefreshUI()
        {
        }

        public void OnUIDestroy()
        {
        }

        public void OnUICreated()
        {
        }

        public override void OnAppFocus(bool focus)
        {

        }

        public override void OnAppQuit()
        {

        }
    }
}
