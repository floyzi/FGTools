using BepInEx.Logging;
using Events;
using FG.Common;
using FG.Common.CMS;
using FGClient;
using FGClient.UI;
using FGClient.UI.Notifications;
using FGTools.States.Logic;
using FGTools.UI;
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
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

        internal static void DoModal(string title, string msg, ModalType type, OKButtonType btnType, Il2CppSystem.Action<bool> act = null, bool doSfx = true, string btnOkStr = null, TextAlignmentOptions al = TextAlignmentOptions.Center, float closeDelay = 0f, ModalHideGUIType hideGUI = ModalHideGUIType.None)
        {
            if (hideGUI > 0)
            {
                if (FGToolsUI.NewGUI.Instance != null && FGToolsUI.NewGUI.Instance.UIRoot != null)
                    FGToolsUI.NewGUI.Instance.ToggleUI(false);
            }

            act += new Action<bool>(wasok =>
            {
                if (hideGUI == ModalHideGUIType.ShowOnCancel)
                {
                    if (!wasok)
                    {
                        UniversalUI.SetUIActive(UniverseGUID, true);
                        FGToolsUI.NewGUI.Instance.UIRoot.gameObject.SetActive(true);
                        FGTStateManager._stateManager.InternalState.LoaderUIToggle = true;
                    }
                    return;
                }

                if (hideGUI != ModalHideGUIType.KeepHidden)
                {
                    UniversalUI.SetUIActive(UniverseGUID, true);
                    FGToolsUI.NewGUI.Instance.UIRoot.gameObject.SetActive(true);
                    FGTStateManager._stateManager.InternalState.LoaderUIToggle = true;
                }

                FGTBase.StateManager.HaveActivePopup = false;
            });

            if (btnOkStr != null)
                AddCMSString("latest_btn_ok", btnOkStr);

            string okStr = btnOkStr == null ? null : $"latest_btn_ok";
            var ModalMessageDataDisclaimer = new ModalMessageData
            {
                Title = title,
                Message = $"<size=80%>{msg}</size>",
                LocaliseTitle = LocaliseOption.NotLocalised,
                LocaliseMessage = LocaliseOption.NotLocalised,
                ModalType = type,
                OkButtonType = btnType,
                OnCloseButtonPressed = act,
                OkTextOverrideId = okStr,
                MessageTextAlignment = al,
                AcceptWaitObservable = ModalMessageBaseData.CreateTimerObservable(closeDelay),
                Priority = PopupMessagePriority.Default,

            };

            PopupManager.Instance.Show(PopupInteractionType.Error, ModalMessageDataDisclaimer);
            if (doSfx)
                AudioManager.PlayOneShot(AudioManager.EventMasterData.GenericPopUpAppears);
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

            text.localScale = new Vector3(0.95f, 1, 1);
            text.GetComponent<TextMeshProUGUI>().m_minFontSize = 6;

            if (string.IsNullOrEmpty(headerCol))
                return;

            ColorUtility.TryParseHtmlString(headerCol.ToUpper(), out var color);
            header.GetComponent<Image>().color = color;
        }

    }
}
