extern alias wle;

using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using FGClient;
using FGClient.UI;
using FGTools.Config;
using FGTools.Content;
using FGTools.HarmonyPatches;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Services.Logic;
using FGTools.States;
using FMODUnity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniverseLib.UI.Models;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.Services.MenuThemeService;
using static Il2CppSystem.Globalization.TimeSpanFormat;
using static UnityEngine.UI.Image;

namespace FGTools.Services
{
    internal class MenuThemeService : FGTService, IFGTGUIHelper
    {
        public class Theme
        {
            public float[] CirclesRGBA { get; set; }
            public float[] UpperGradientRGBA { get; set; }
            public float[] LowerGradientRGBA { get; set; }
            public string Pattern { get; set; }
            public string LoopMusic { get; set; }
            public float IntroLength { get; set; }
            public float EndCutoff { get; set; }
            public float VolumeModifier { get; set; }
            public string DisplayName { get; set; }
        }

        public class Theme3D
        {
            public string SceneName { get; set; }
            public float[] CamPos { get; set; }
            public float[] CamRot { get; set; }
        }

        class FallGuysTheme
        {
            public Sprite BackdropImage;
            public Color BackdropUpper;
            public Color BackdropLower;
            public Color BackdropColor;
            public Color CirclesImageColor;
            public Color CirclesColor;
            public Texture2D PatternTexture;
            public Color GradientColor;
            public bool Was3d;

            public void Load()
            {
                var currentBG = GameObject.Find(CurrentFGBackground);
                var mask = currentBG.transform.GetChild(1);

                Image backdrop = mask.GetChild(0).GetComponent<Image>();
                backdrop.sprite = BackdropImage;
                backdrop.color = BackdropColor;

                mask.GetChild(2).GetComponent<Image>().color = CirclesImageColor;
                Material circlesMaterial = mask.GetChild(2).GetComponent<Image>().material;
                circlesMaterial.color = CirclesColor;
                circlesMaterial.SetTexture("_Pattern", PatternTexture);


                Image gradient = mask.GetChild(3).GetComponent<Image>();
                gradient.gameObject.SetActive(false);
                gradient.color = GradientColor;
            }

            public Sprite GetPattern()
            {
                var tex = GetTextureCopy(PatternTexture);
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }

        Dropdown themesDropdown;
        Dropdown webthemesDropdown;
        Il2CppSystem.Collections.Generic.List<string> themes = new();
        Il2CppSystem.Collections.Generic.List<string> themesDirs = new();
        Image UpperGradient;
        Image gradient;
        Image image;
        Image pattern;
        public string ThemeOnPreviewPath;
        public string CurrentThemePath = ConfigManager.InGameTheme.Value;
        public Theme ThemeOnPreview;
        MenuAudioProvider AudioProvider;
        ButtonRef SelectButton;
        public string ThemeDir
        {
            get
            {
                if (CurrentThemePath == string.Empty)
                    return string.Empty;
                else
                    return CurrentThemePath.Split('\\')[0];
            }
        }
        string currentUrl;
        string folderName;
        string author;
        bool deleteFolderFirst = false;
        GameObject themeActions;
        ButtonRef delThemeDirBtn;
        public bool IsOnDefaultTheme = true;
        string themesInf;
        Text themesInfoString;
        List<string> WebThemesIds = [];
        static FallGuysTheme DefaultTheme;
        internal static Material BackgroudMaterial;

        public override void RegisterService()
        {

        }

        public override void UpdateService()
        {
            if (BackgroudMaterial == null && StateManager.ActiveState is MenuState)
                SetDefaultBackground();
        }

        static Texture2D GetTextureCopy(Texture source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0);

            Graphics.Blit(source, rt);
            Texture2D tex = new(source.width, source.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            tex.name += "_Copy";
            tex.hideFlags = HideFlags.HideAndDontSave;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

      
        static Sprite PNGtoSprite(string path)
        {
            if (File.Exists(path))
            {
                byte[] imagedata = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(0, 0, TextureFormat.ARGB32, false);
                texture.LoadImage(imagedata);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 0));
                return sprite;
            }
            return null;
        }

        public void SelectTheme(bool userSelect, bool skipMus = false)
        {
            if (!userSelect)
            {
                FMODTool.UnloadBank("BNK_Music_MainMenu");
                SetTheme(CurrentTheme, GameObject.Find(CurrentFGBackground));
                SetThemeForLoadingScreens();
                if (!skipMus)
                    OnMenuPlayMusicEvent();
                return;
            }

            if (ThemeOnPreviewPath == null)
                return;

            SelectButton.Component.interactable = false;
            if (ThemeOnPreviewPath != LocalizedStr("gui_default"))
            {
                Resources.FindObjectsOfTypeAll<MainMenuManager>().FirstOrDefault().StopMusic(true);
                ConfigManager.InGameTheme.Value = ThemeOnPreviewPath;
                CurrentTheme = ThemeOnPreview;
                CurrentThemePath = ThemeOnPreviewPath;
                LoadThemeFromPreview();
            }
            else
            {
                if (DefaultTheme == null)
                    return;

                CurrentTheme = null;
                CurrentThemePath = null;
                ConfigManager.InGameTheme.Value = LocalizedStr("gui_default");
                if (Plugin.ThemesHarmonyPatched)
                {
                    Plugin.ThemesHarmony.UnpatchSelf();
                    Plugin.ThemesHarmonyPatched = false;
                }

                GameObject.Destroy(AudioProvider);
                DefaultTheme.Load();
                var a = Resources.FindObjectsOfTypeAll<MainMenuManager>().FirstOrDefault();
                a?.ResumeMusic();
            }

            if (!skipMus)
                OnMenuPlayMusicEvent();
        }

        public void OnMenuEnterEvent()
        {
            SetDefaultBackground();

            if (CurrentTheme == null)
                return;

            Hide3DBG();
            OnMenuSetThemeEvent(true);
            Instance.StartCoroutine(PlayMusic().WrapToIl2Cpp());
        }

        IEnumerator PlayMusic()
        {
            yield return new WaitForEndOfFrame();
            OnMenuPlayMusicEvent();
            yield return new WaitForSeconds(0.2f);
            var a = Resources.FindObjectsOfTypeAll<MainMenuManager>().FirstOrDefault();
            a._menuMusic.Stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            FMODTool.UnloadBank("BNK_Music_MainMenu");
        }

        public void OnMenuSetThemeEvent(bool skipMus)
        {
            if (ConfigManager.InGameTheme.Value == LocalizedStr("gui_default"))
                return;

            if (CurrentTheme != null)
                SelectTheme(false, skipMus);
        }

        static FallGuysTheme SaveDefaultTheme()
        {
            if (DefaultTheme != null) return DefaultTheme;

            var result = new FallGuysTheme();
            var currentBG = GameObject.Find(CurrentFGBackground);

            if (currentBG == null)
                return null;


            var mask = currentBG.transform.GetChild(1);

            var backdrop = mask.GetChild(0).GetComponent<Image>();

            var og = backdrop.sprite.texture;
            var temp = RenderTexture.GetTemporary(og.width, og.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
            Graphics.Blit(og, temp);
            RenderTexture.active = temp;
            Texture2D res = new(og.width, og.height, og.format, false)
            {
                name = og.name
            };
            res.ReadPixels(new Rect(0, 0, temp.width, temp.height), 0, 0);
            res.Apply();
            RenderTexture.ReleaseTemporary(temp);

            var cen = res.width / 2;
            result.BackdropUpper = res.GetPixel(cen, res.height - 1);
            result.BackdropLower = res.GetPixel(cen, 0);

            var safe = Sprite.Create(res, new Rect(0, 0, og.width, og.height), backdrop.sprite.pivot, backdrop.sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            safe.hideFlags = HideFlags.HideAndDontSave;
            result.BackdropImage = safe;
            result.BackdropColor = backdrop.color;

            result.CirclesImageColor = mask.GetChild(2).GetComponent<Image>().color;
            var circlesMaterial = mask.GetChild(2).GetComponent<Image>().material;

            BackgroudMaterial = new(circlesMaterial);

            result.CirclesColor = circlesMaterial.color;
            result.PatternTexture = GetTextureCopy(circlesMaterial.GetTexture("_Pattern"));

            var gradient = mask.GetChild(3).GetComponent<Image>();
            result.GradientColor = gradient.color;
            return result;
        }

        static void SetDefaultBackground()
        {
            DefaultTheme ??= SaveDefaultTheme();

            var currentBG = GameObject.Find(CurrentFGBackground);

            if (currentBG == null)
                return;

            var mask = currentBG.transform.GetChild(1);

            var backdrop = mask.GetChild(0).GetComponent<Image>();
            var circlesMaterial = mask.GetChild(2).GetComponent<Image>().material;

            if (DefaultTheme.BackdropImage == null)
                DefaultTheme.BackdropImage = backdrop.sprite;

            if (DefaultTheme.PatternTexture == null)
                DefaultTheme.PatternTexture = GetTextureCopy(circlesMaterial.GetTexture("_Pattern"));

            BackgroudMaterial = new(circlesMaterial);
        }

        public void OnMenuPlayMusicEvent()
        {
            if (ConfigManager.InGameTheme.Value == LocalizedStr("gui_default"))
                return;

            var a = Resources.FindObjectsOfTypeAll<MainMenuManager>().FirstOrDefault();

            AudioProvider = a.gameObject.GetComponent<MenuAudioProvider>() ?? a.gameObject.AddComponent<MenuAudioProvider>();
            AudioProvider.PlayMusic(false);

        }

        void Hide3DBG()
        {
            var assBG = Resources.FindObjectsOfTypeAll<Menu3DBackgroundOptionViewModel>().FirstOrDefault();
            var menuBG = Resources.FindObjectsOfTypeAll<MainMenuBackgroundViewModel>().FirstOrDefault();

            assBG.Enable3DMenuBackground = false;
            assBG.Save();

            menuBG.Hide3dBackground();
            menuBG.TryStopFadeCoroutines();
            //try { UnityEngine.Object.Destroy(Resources.FindObjectsOfTypeAll<wle.LevelEditorThemeLighting>().FirstOrDefault().gameObject); } catch { };
        }

        public void LoadThemeFromPreview()
        {
            if (!Plugin.ThemesHarmonyPatched)
            {
                Plugin.ThemesHarmony.PatchAll(typeof(ThemePatches));
                Plugin.ThemesHarmonyPatched = true;
            }
            if (ThemeOnPreview != null)
            {
                Hide3DBG();
                FMODTool.UnloadBank("BNK_Music_MainMenu");
                SetTheme(ThemeOnPreview, GameObject.Find(CurrentFGBackground));

                if (CurrentThemePath == ThemeOnPreviewPath)
                    delThemeDirBtn.Component.interactable = false;
            }
            SetThemeForLoadingScreens();
        }

        [Obsolete("remnant of a feature that i was too lazy to implement")]
        public void Set3DTheme(Theme3D theme)
        {
            GameObject.Find(CurrentFGBackground).SetActive(false);
            Addressables.LoadScene(theme.SceneName, LoadSceneMode.Additive);
        }

        public void PreviewTheme(int index)
        {
            IsOnDefaultTheme = index == 0;
            SelectButton.Component.interactable = true;

            if (index > 0)
            {
                themeActions.gameObject?.SetActive(true);

                ThemeOnPreviewPath = themesDirs[index - 1];
                string themeString = File.ReadAllText($"{Plugin.ThemesDir}{themesDirs[index - 1]}");
                ThemeOnPreview = JsonSerializer.Deserialize<Theme>(themeString);

                if (CurrentTheme != null)
                    delThemeDirBtn.Component.interactable = ThemeOnPreview.DisplayName != CurrentTheme.DisplayName;

                SelectButton.Component.interactable = CurrentTheme == null ? true : ThemeOnPreview.DisplayName != CurrentTheme.DisplayName;
                SetThemeForPreview(ThemeOnPreview);
            }
            else
            {
                themeActions.gameObject.SetActive(false);
                ThemeOnPreviewPath = LocalizedStr("gui_default");
                SetThemeForPreview(null);
            }
        }

        public void SetThemeForPreview(Theme theme)
        {
            Color upper;
            Color lower;
            Sprite pat;
            Color patCol;

            if (theme == null)
            {
                upper = DefaultTheme.BackdropUpper;
                lower = DefaultTheme.BackdropLower;
                pat = DefaultTheme.GetPattern();
                patCol = DefaultTheme.CirclesImageColor;
            }
            else
            {
                upper = new(theme.UpperGradientRGBA[0], theme.UpperGradientRGBA[1], theme.UpperGradientRGBA[2], theme.UpperGradientRGBA[3]);
                lower = new(theme.LowerGradientRGBA[0], theme.LowerGradientRGBA[1], theme.LowerGradientRGBA[2], theme.LowerGradientRGBA[3]);
                pat = PNGtoSprite($"{Plugin.ThemesDir}/{Path.GetDirectoryName(ThemeOnPreviewPath)}/{theme.Pattern}");
                patCol = new(theme.CirclesRGBA[0], theme.CirclesRGBA[1], theme.CirclesRGBA[2], theme.CirclesRGBA[3]);
            }

            if (UpperGradient != null)
                UpperGradient.color = upper;

            if (gradient != null)
                gradient.color = lower;

            if (pattern == null)
                return;

            pattern.color = Color.white;
            BackgroudMaterial.color = patCol;
            BackgroudMaterial.SetTexture("_Pattern", pat.texture);

            //not proud of this one
            pattern.material = new(BackgroudMaterial);
        }

        public void SetThemeForLoadingScreens()
        {
            if (File.Exists($"{Plugin.ThemesDir}/{ConfigManager.InGameTheme.Value}") && ConfigManager.InGameTheme.Value != LocalizedStr("gui_default"))
            {
                foreach (LoadingGameScreenViewModel loadingGameScreen in Resources.FindObjectsOfTypeAll<LoadingGameScreenViewModel>())
                {
                    if (loadingGameScreen.name == "Prime_UI_RoundSelected_Prefab_Canvas")
                    {
                        SetTheme(CurrentTheme, loadingGameScreen.transform.GetChild(1).GetChild(0).gameObject);
                    }
                }
                foreach (LoadingUGCGameScreenViewModel loadingGameScreen in Resources.FindObjectsOfTypeAll<LoadingUGCGameScreenViewModel>())
                {
                    if (loadingGameScreen.name == "Prime_UI_RoundSelected_UGC_Prefab_Canvas")
                    {
                        SetTheme(CurrentTheme, loadingGameScreen.transform.GetChild(1).GetChild(0).gameObject);
                    }
                }

            }
            else if (ConfigManager.InGameTheme.Value == LocalizedStr("gui_default"))
            {
                if (Plugin.ThemesHarmonyPatched)
                {
                    Plugin.ThemesHarmony.UnpatchSelf();
                    Plugin.ThemesHarmonyPatched = false;
                }
            }
        }

        public void SetTheme(Theme theme, GameObject gameObject)
        {
            if (FGTTargetSettings.CustomThemes && ConfigManager.InGameTheme.Value != LocalizedStr("gui_default") && File.Exists($"{Plugin.ThemesDir}/{ConfigManager.InGameTheme.Value}"))
            {
                string themeString = File.ReadAllText($"{Plugin.ThemesDir}/{ConfigManager.InGameTheme.Value}");
                CurrentTheme = JsonSerializer.Deserialize<Theme>(themeString);

                Sprite pattern = PNGtoSprite($"{Plugin.ThemesDir}/{Path.GetDirectoryName(CurrentThemePath)}/{theme.Pattern}");
                Transform mask = gameObject.transform.GetChild(1);
                if (theme.UpperGradientRGBA != null)
                {
                    Image backdrop = mask.GetChild(0).GetComponent<Image>();
                    backdrop.sprite = null;
                    backdrop.color = new Color(theme.UpperGradientRGBA[0], theme.UpperGradientRGBA[1], theme.UpperGradientRGBA[2], theme.UpperGradientRGBA[3]);
                }

                if (theme.CirclesRGBA != null)
                {
                    mask.GetChild(2).GetComponent<Image>().color = Color.white;
                    Material circlesMaterial = mask.GetChild(2).GetComponent<Image>().material;
                    circlesMaterial.color = new Color(theme.CirclesRGBA[0], theme.CirclesRGBA[1], theme.CirclesRGBA[2], theme.CirclesRGBA[3]);
                    circlesMaterial.SetTexture("_Pattern", pattern.texture);
                }

                if (theme.LowerGradientRGBA != null)
                {
                    Image gradient = mask.GetChild(3).GetComponent<Image>();
                    gradient.gameObject.SetActive(true);
                    gradient.color = new Color(theme.LowerGradientRGBA[0], theme.LowerGradientRGBA[1], theme.LowerGradientRGBA[2], theme.LowerGradientRGBA[3]);
                }
            }
        }

        public void DeleteThemeAction()
        {
            DoModal(LocalizedStr("gui_theme_delete_title", [ThemeOnPreview.DisplayName]), LocalizedStr("gui_theme_delete_desc"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Disruptive, new Action<bool>((bool wasok) =>
            {
                if (wasok)
                {
                    var target = Plugin.ThemesDir + ThemeOnPreviewPath.Split('\\')[0];
                    foreach (var file in Directory.GetFiles(target))
                        File.Delete(file);
                    Directory.Delete(target);
                    PreviewTheme(0);
                    RefreshThemesDropdown();
                }
            }), hideGUI: ModalHideGUIType.KeepHiddenForThisModal);
        }

        public void PickOnlineTheme(int index)
        {
            selectWebTheme.Component.interactable = false;

            if (index == 0)
            {
                txt.text = $"{LocalizedStr("gui_catalogue_theme_info_0")}\n{LocalizedStr("gui_catalogue_theme_info_1")}\n\n{LocalizedStr("gui_catalogue_theme_info_2")}\n{LocalizedStr("gui_catalogue_theme_info_3")}";
                return;
            }

            string themeKey = WebThemesIds[index - 1];
            string toInfo = string.Empty;
            int idk = 0;
            bool exists = false;

            if (OnlineCheck.FGTContent.ThemeData.TryGetValue(themeKey, out var webTheme))
            {
                currentUrl = webTheme.DownloadURL;
                folderName = webTheme.FolderName;
                author = webTheme.Credit;
                exists = FGTServiceManager.GetService<MenuThemeService>().CurrentThemePath != null && FGTServiceManager.GetService<MenuThemeService>().ThemeDir == folderName;

                toInfo = $"{LocalizedStr("gui_theme2download")}: {folderName}\n{LocalizedStr("gui_author")}: {author}\n{LocalizedStr("gui_theme2download_url")}: {currentUrl}\n{LocalizedStr("gui_theme2download_size")}: {LocalizedStr("gui_wait")}";
            }
            else
                currentUrl = null;

            if (folderName != null && Directory.Exists(Plugin.ThemesDir + folderName))
            {
                idk++;
                toInfo += $"\n\n<color=yellow>{LocalizedStr("gui_catalogue_theme_exists_new")}</color>";
                deleteFolderFirst = true;
            }
            else
                deleteFolderFirst = false;

            if (exists)
            {
                string target;
                if (idk > 0)
                    target = "\n\n";
                else
                    target = "\n";

                toInfo += $"{target}<color=yellow>{LocalizedStr("gui_theme_selected_error")}</color>";
            }

            txt.text = toInfo;

            CoroutineRunner.Instance.StartCoroutine(CalculateThemeSize(currentUrl, !exists).WrapToIl2Cpp());
        }

        IEnumerator CalculateThemeSize(string url, bool canBeSelected = true)
        {
            UnityWebRequest request = UnityWebRequest.Head(url);
            yield return request.SendWebRequest();

            selectWebTheme.Component.interactable = canBeSelected;
            AudioManager.PlayOneShot(AudioManager.Instance._eventMasterData.GenericMove);

            if (request.result != UnityWebRequest.Result.Success)
            {
                string loaded = txt.text.Replace(LocalizedStr("gui_wait"), $"{LocalizedStr("gui_unknown")}");
                txt.text = loaded;
            }
            else
            {
                if (long.TryParse(request.GetResponseHeader("Content-Length"), out long fileSize))
                    txt.text = txt.text.Replace(LocalizedStr("gui_wait"), CalculateSizeString(fileSize));
            }

            request.Dispose();
        }

        public void startDownloading() => CoroutineRunner.Instance.StartCoroutine(DownloadTheme().WrapToIl2Cpp());

        bool didStartNotif = false;
        int currThemeIndx = -1;
        public void RefreshThemesDropdown()
        {
            themesDropdown.ClearOptions();
            themes.Clear();
            themesDirs.Clear();
            themes.Add(LocalizedStr("gui_default"));
            string[] dirs = Directory.GetDirectories($"{Plugin.ThemesDir}");
            foreach (string dir in dirs)
            {
                foreach (string file in Directory.GetFiles(dir))
                {
                    if (file.EndsWith(".json"))
                    {
                        var target = Path.Combine(Path.GetDirectoryName(Path.GetRelativePath($"{Plugin.ThemesDir}", file)), Path.GetFileName(file));
                        themesDirs.Add(target);
                        var a = JsonSerializer.Deserialize<MenuThemeService.Theme>(File.ReadAllText(file));
                        themes.Add(a.DisplayName);
                    }
                }
            }
            themesDropdown.AddOptions(themes);

            if (CurrentTheme != null)
            {
                currThemeIndx = themes.IndexOf(CurrentTheme.DisplayName);
                themesDropdown.value = currThemeIndx;
            }

            PreviewTheme(currThemeIndx);
        }

        System.Collections.IEnumerator DownloadTheme()
        {
            UnityWebRequest www = UnityWebRequest.Get(currentUrl);

            UnityWebRequestAsyncOperation operation = www.SendWebRequest();

            yield return DownloadProgress(operation);

            if (www.result != UnityWebRequest.Result.Success)
            {
                didStartNotif = false;
                CreateNotification(LocalizedStr("gui_theme_download_error"), www.error, FGT_Error_Color);
                txt.text = $"{LocalizedStr("gui_theme_download_error")}\n\n{LocalizedStr("gui_theme_download_error_0")}:\n{LocalizedStr("gui_theme_download_error_1")}\n{LocalizedStr("gui_theme_download_error_2")}\n{LocalizedStr("gui_theme_download_error_3")}\n{LocalizedStr("gui_theme_download_error_4")}\n\n({LocalizedStr("gui_error_we_got")}: {www.error} | {www.result})";
                selectWebTheme.Component.interactable = true;
                webthemesDropdown.interactable = true;
                yield break;
            }

            if (Directory.Exists(Plugin.ThemesDir + folderName) && deleteFolderFirst)
            {
                foreach (string file in Directory.GetFiles(Plugin.ThemesDir + folderName))
                    File.Delete(file);

                Directory.Delete(Plugin.ThemesDir + folderName);
                deleteFolderFirst = false;
            }

            didStartNotif = false;
            string zipPath = $"{Plugin.AssetsDir}{folderName}.zip";
            File.WriteAllBytes(zipPath, www.downloadHandler.data);
            ZipFile.ExtractToDirectory(zipPath, Plugin.ThemesDir);
            File.Delete(zipPath);
            RefreshThemesDropdown();
            themeActions.gameObject.SetActive(false);
            CreateNotification(LocalizedStr("gui_download_end_title"), LocalizedStr("gui_theme_download_end_info", [folderName]), FGT_Info_Color);
            txt.text = $"{LocalizedStr("gui_theme_download_complete_0", [folderName])}\n{LocalizedStr("gui_theme_download_complete_1")}";
            //selectWebTheme.Component.interactable = false;
            webthemesDropdown.interactable = true;
        }

        float loadProgress;
        ButtonRef selectWebTheme;
        Text txt;
        IEnumerator DownloadProgress(UnityWebRequestAsyncOperation operation)
        {
            while (!operation.isDone)
            {
                if (!didStartNotif)
                {
                    CreateNotification(LocalizedStr("gui_download_start_title"), LocalizedStr("gui_theme_download_start_info", [folderName]), FGT_Info_Color);
                    didStartNotif = true;
                }

                loadProgress = operation.progress * 100;
                txt.text = $"{LocalizedStr("gui_theme_download_progress", [folderName])}\n{LocalizedStr("gui_complete")}: {loadProgress:F2}%";
                selectWebTheme.Component.interactable = false;
                webthemesDropdown.interactable = false;
                yield return null;
            }

            loadProgress = 0;
        }

        public override void DrawGUI()
        {
        }

        public void UpdateInfo()
        {
            themesInf = $"{LocalizedStr("gui_themes_stat_0")}: {themesDropdown.options.Count - 1} | {LocalizedStr("gui_themes_stat_1")}: {WebThemesIds.Count}";
            themesInfoString.text = themesInf;
        }

        public void SetUIReferences(object[] data)
        {
            selectWebTheme = (ButtonRef)data[0];
            themeActions = (GameObject)data[1];
            txt = (Text)data[2];
            delThemeDirBtn = (ButtonRef)data[3];
            themesDropdown = (Dropdown)data[5];
            UpperGradient = (Image)data[6];
            webthemesDropdown = (Dropdown)data[7];
            gradient = (Image)data[8];
            themesInfoString = (Text)data[9];
            WebThemesIds = (List<string>)data[10];
            pattern = (Image)data[11];
            SelectButton = (ButtonRef)data[12];

            RefreshThemesDropdown();
        }

        public void RefreshUI()
        {

        }

        public Theme CurrentTheme;
    }
}
