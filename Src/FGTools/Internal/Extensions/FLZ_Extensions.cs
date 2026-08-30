using BepInEx.Logging;
using Catapult.Network.Eula;
using Catapult.Network.RemoteServices.HttpRequests.Eula;
using Events;
using FG.Common;
using FG.Common.CMS;
using FGClient;
using FGClient.UI;
using FGClient.UI.Core;
using FGClient.UI.Notifications;
using FGTools.LocalServer.Implementations;
using FGTools.States.Logic;
using FGTools.UI;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UniverseLib.UI;
using static FGClient.UI.UIModalMessage;
using static FGTools.Services.LocalizationService;

namespace FGTools.Internal.Extensions
{
    internal static class FLZ_Extensions
    {
        internal static ManualLogSource logSource = new(Launcher.DisplayName);
        public enum ModalHideGUIType
        {
            None,
            KeepHidden,
            KeepHiddenForThisModal,
            ShowOnCancel,
        }

        internal static IEnumerator LoadObject<T>(string name, Action<T> res)
        {
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(name);

            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                res.Invoke(handle.Result);
            }
            else
            {
                FGTLog(LogLevel.Error, null, $"Load fail... {handle.Status}");
            }
        }

        internal static void FGTLog(LogLevel logType = LogLevel.Info, object sender = null, object content = null)
        {
            if (sender != null)
            {
                if (sender is Type type)
                    logSource.Log(logType, $"[{type.Name}] {content}");

                else if (sender is string str)
                    logSource.Log(logType, $"[{str}()] {content}");
            }
            else
                logSource.Log(logType, $"{content}");
        }

        internal static void QuitWithMessage(string title, string msg)
        {
            Application.Quit();
            _ = Launcher.MessageBox(IntPtr.Zero, msg, title, 0x00000010);
        }

        internal static void LaunchCMDWithArgs(string args)
        {
            System.Diagnostics.Process process = new();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
        }

        internal static void ForceExit() => GlobalGameStateClient.Instance._gameStateMachine.ReplaceCurrentState(new StateReloadingToMainMenu(GlobalGameStateClient.Instance._gameStateMachine, GlobalGameStateClient.Instance.CreateClientGameStateData()).Cast<GameStateMachine.IGameState>());

        internal struct FLZ_ModalData
        {
            public string Title;
            public string Message;
            public ModalType Type;
            public OKButtonType OKButton;
            public Action<bool> OnClick;
            public Action<bool> OnClosed;
            public Il2CppSystem.IObservable<Unit> CloseDelay;
            public string OKStrOverride;
            public TextAlignmentOptions TextAlignment;
            public ModalHideGUIType HideGUIType;
            public int Priority;

            public FLZ_ModalData(string title, string message, ModalHideGUIType hideLvl, int priority = 200)
            {
                Title = title;
                Message = message;
                Type = ModalType.MT_OK;
                OKButton = OKButtonType.Default;
                HideGUIType = hideLvl;
                Priority = priority;
            }

            public FLZ_ModalData(string title, string message, ModalType type, OKButtonType okType, Action<bool> onClick = null, Action<bool> onClosed = null, string okStrOverride = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center, float closeDelay = -1, ModalHideGUIType hideLvl = ModalHideGUIType.None, int priority = 200)
            {
                Title = title;
                Message = message;
                Type = type;
                OKButton = okType;
                OnClick = onClick;
                OnClosed = onClosed;
                OKStrOverride = okStrOverride;
                TextAlignment = alignment;

                if (closeDelay > 0)
                    CloseDelay = ModalMessageBaseData.CreateTimerObservable(closeDelay);

                HideGUIType = hideLvl;
                Priority = priority;
            }
        }

        internal static void DoModal(FLZ_ModalData data)
        {
            if (PopupManager.Instance.HasActivePopup)
                PopupManager.Instance.HideActivePopup();

            if (data.HideGUIType > 0)
            {
                if (FGToolsUI.Instance != null && FGToolsUI.Instance.UIRoot != null)
                    FGToolsUI.Instance.ToggleUI(false);
            }

            data.OnClick += new Action<bool>(wasok =>
            {
                if (data.HideGUIType == ModalHideGUIType.ShowOnCancel)
                {
                    if (!wasok)
                    {
                        FGToolsUI.Instance.ToggleUI(true);
                    }
                    return;
                }

                if (data.HideGUIType != ModalHideGUIType.KeepHidden)
                {
                    UniversalUI.SetUIActive(UniverseGUID, true);
                    FGToolsUI.Instance.UIRoot.gameObject.SetActive(true);
                    FGTStateManager._stateManager.InternalState.LoaderUIToggle = true;
                }
            });

            if (!string.IsNullOrEmpty(data.OKStrOverride))
                AddCMSString("latest_btn_ok", data.OKStrOverride);

            string okStr = string.IsNullOrEmpty(data.OKStrOverride) ? null : $"latest_btn_ok";
            var ModalMessageDataDisclaimer = new ModalMessageData
            {
                Title = data.Title,
                Message = data.Message,
                LocaliseTitle = LocaliseOption.NotLocalised,
                LocaliseMessage = LocaliseOption.NotLocalised,
                ModalType = data.Type,
                OkButtonType = data.OKButton,
                OnCloseButtonPressed = data.OnClick,
                OkTextOverrideId = okStr,
                MessageTextAlignment = data.TextAlignment,
                AcceptWaitObservable = data.CloseDelay,
                OnClosed = data.OnClosed,
                Priority = (PopupMessagePriority)data.Priority,

            };

            PopupManager.Instance.Show(PopupInteractionType.Error, ModalMessageDataDisclaimer);
        }

        internal static Sprite GetSpriteFromFile(string path, int Width, int Height)
        {
            if (!File.Exists(path))
                return null;

            var bytes = File.ReadAllBytes(path);
            Texture2D tex = new(Width, Height, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
            {
                tex.filterMode = FilterMode.Point;
                var spr = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                spr.name = Path.GetFileNameWithoutExtension(path);
                return spr;
            }
            return null;
        }

        internal static Sprite SetSpriteFromBytes(byte[] bytes, int Width, int Height)
        {
            Texture2D Texture = new(Width, Height, TextureFormat.RGBA32, false);
            if (Texture.LoadImage(bytes))
            {
                Texture.filterMode = FilterMode.Point;
                return Sprite.Create(Texture, new Rect(0.0f, 0.0f, Texture.width, Texture.height), new Vector2(0.5f, 0.5f));
            }
            return null;
        }

        internal static LocalisedString AddCMSString(string key, string value)
        {
            if (CMSLoader.Instance._localisedStrings.ContainsString(key))
                CMSLoader.Instance._localisedStrings._localisedStrings.Remove(key);

            CMSLoader.Instance._localisedStrings._localisedStrings.Add(key, value);
            return new()
            {
                Id = key,
                Text = value,
            };
        }

        internal static GameObject GetChild(GameObject Parent, string Name)
        {
            foreach (Transform Transform in Parent.GetComponentsInChildren<Transform>(true))
                if (Transform.name == Name) return Transform.gameObject;
            return null;
        }

        internal static string CleanStr(string strIN, bool rpcFormat = false)
        {
            string strOUT = Regex.Replace(strIN, @"<.*?>|\t|\s{2,}", " ");
            strOUT = Regex.Replace(strOUT, @"(?<=<) | (?=>)", "");
            strOUT = strOUT.Trim();
            strOUT = Regex.Replace(strOUT, @"\s+", " ");
            if (rpcFormat)
            {
                string[] words = strOUT.Split(' ');
                for (int i = 0; i < words.Length; i++)
                    words[i] = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(words[i].ToLower());
                strOUT = string.Join(" ", words);
            }
            return strOUT;
        }

        internal static long CalculateDirSize(string path)
        {
            long size = 0;

            foreach (FileInfo file in new DirectoryInfo(path).GetFiles())
            {
                size += file.Length;
            }

            foreach (DirectoryInfo directory in new DirectoryInfo(path).GetDirectories())
            {
                size += CalculateDirSize(directory.FullName);
            }

            return size;
        }

        internal static string CalculateSizeString(long size)
        {
            string[] sizes = ["KB", "MB", "GB"];
            double len = size / 1024.0;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        internal static void XorByteArray(ref byte[] data, byte[] key)
        {
            int num = data.Length;
            int num2 = key.Length;
            for (int i = 0; i < num; i++)
            {
                byte[] array = data;
                int num3 = i;
                array[num3] ^= key[i % num2];
            }
        }

        internal static bool BytesCheck(byte[] arr1, byte[] arr2)
        {
            if (arr1.Length < arr2.Length)
                return false;

            for (int i = 0; i < arr1.Length; i++)
            {
                if (arr1[i] != arr2[i])
                    return false;
            }

            return true;

        }

        internal static T GetItem<T>(string id) where T : ScriptableObject
        {
            foreach (T option in Resources.FindObjectsOfTypeAll<T>())
            {
                if (option.name == id)
                    return option;
            }
            return null;
        }

        internal static string GetItemName(string id)
        {
            foreach (ItemDefinitionSO option in Resources.FindObjectsOfTypeAll<ItemDefinitionSO>())
            {
                if (option.name == id)
                    return option.Cast<ItemDefinitionSO>().DisplayName;
            }

            foreach (LocalisedStrings str in Resources.FindObjectsOfTypeAll<LocalisedStrings>())
            {
                foreach (var pair in str._localisedStrings)
                {
                    if (pair.key == id)
                        return pair.value;
                }
            }

            return LocalizedStr("gui_unable_get_name");
        }

        internal static byte[] GetFileInZip(byte[] zipContent, string file)
        {
            if (zipContent == null ||  zipContent.Length == 0) return null;
            using var zipStream = new MemoryStream(zipContent);
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
            var localeFile = zip.Entries.FirstOrDefault(e => e.Name == file);
            byte[] loadedBytes = new BinaryReader(localeFile.Open()).ReadBytes((int)localeFile.Length);
            return loadedBytes;
        }

        internal static void CreateNotification(string title, string msg, string headerCol = null, float durination = -1, Action onComplete = null)
        {
            var dat = new TextNotificationData(null)
            {
                Title = title,
                Message = msg,
            };

            if (durination > 0)
                dat._Duration_k__BackingField = durination;

            if (onComplete != null)
                dat.OnComplete += onComplete;

            NotificationManager.Instance.ShowNotificationImmediately(dat);

            var target = NotificationManager.Instance._activeNotifications[NotificationManager.Instance.ActiveNotifications - 1].gameObject;
            var text = target.transform.GetChild(3);
            var header = target.transform.GetChild(1).GetChild(0);

            text.localScale = new Vector3(0.90f, 0.90f, 0.90f);

            var tmp = text.GetComponent<TextMeshProUGUI>();
            tmp.fontSizeMax = 25;
            tmp.fontSizeMin = 14;

            if (string.IsNullOrEmpty(headerCol))
                return;

            ColorUtility.TryParseHtmlString(headerCol.ToUpper(), out var color);
            header.GetComponent<Image>().color = color;
        }

        internal static bool TryRegisterTypeInIl2cpp<T>() where T : class
        {
            if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
            {
                ClassInjector.RegisterTypeInIl2Cpp<T>();
                return true;
            }

            return false;
        }

        public static void CreateEULAModal(string title, string content, Action<bool> onClick, bool oneBtn = false)
        {
            if (UIManager.Instance.GetScreen<EULAPopupViewModel>(ScreenStackType.Popup) != null)
                return;

            var localisedBckp = new Il2CppSystem.Collections.Generic.Dictionary<string, string>();

            foreach (var str in CMSLoader.Instance._localisedStrings._localisedStrings)
            {
                localisedBckp.Add(str.key, str.value);
            }

            UIManager.Instance.ShowScreen<EULAPopupViewModel>(new()
            {
                Data = new EulaDetails(new HttpGetNewEulaResponse()
                {
                    Title = title,
                    Body = content,
                    Version = 1,
                    Key = "fallguys",
                    Locale = "en",
                }),
                ScreenStack = ScreenStackType.Popup,
                UseScrim = true,
                OnClosedAction = new Action(() =>
                {
                    //it just removes specific strings for no reason at all, not wasting my time to figure it out
                    CMSLoader.Instance._localisedStrings._localisedStrings = localisedBckp;
                })
            });

            var inst = UIManager.Instance.GetScreen<EULAPopupViewModel>(ScreenStackType.Popup);
            inst.gameObject.transform.GetChild(3).gameObject.SetActive(false);
            inst.gameObject.transform.GetChild(4).gameObject.SetActive(false);

            foreach (var btn in inst.gameObject.transform.GetChild(2).transform.GetComponentsInChildren<UIButtonSolo>())
            {
                bool isAccept = btn.name.Contains("GlyphTextCalltoActionButton");

                btn._onClick.AddListener(new Action(() =>
                {
                    onClick?.Invoke(isAccept);
                    inst.OnAgreementAccepted();
                }));

                if (!isAccept && oneBtn)
                    btn.gameObject.SetActive(false);
            }

            inst.UpdateText(false);
        }
    }
}
