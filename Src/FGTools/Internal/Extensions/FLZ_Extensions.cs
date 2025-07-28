using BepInEx.Logging;
using Events;
using FG.Common.CMS;
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
    public static class FLZ_Extensions
    {
        public static ManualLogSource logSource = new(Plugin.DisplayName);
        public enum ModalHideGUIType
        {
            None,
            KeepHidden,
            KeepHiddenForThisModal,
            ShowOnCancel,
        }

        public static IEnumerator LoadObject<T>(string name, Action<T> res)
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

        public static void FGTLog(LogLevel logType = LogLevel.Info, object sender = null, object content = null)
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

        public static void LaunchCMDWithArgs(string args)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
        }

        public static void DoModal(string title, string msg, ModalType type, OKButtonType btnType, Il2CppSystem.Action<bool> act = null, bool doSfx = true, string btnOkStr = null, TextAlignmentOptions al = TextAlignmentOptions.Center, float closeDelay = 0f, ModalHideGUIType hideGUI = ModalHideGUIType.None)
        {
            if (hideGUI > 0)
            {
                if (FGToolsUI.NewGUI.Instance != null && FGToolsUI.NewGUI.Instance.UIRoot != null)
                {
                    UniversalUI.SetUIActive(UniverseGUID, false);
                    FGToolsUI.NewGUI.Instance.UIRoot.gameObject.SetActive(false);
                }
                FGTStateManager._stateManager.InternalState.LoaderUIToggle = false;
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

            Il2CppSystem.IObservable<UniRx.Unit> acceptWaitObs = ModalMessageBaseData.CreateTimerObservable(closeDelay);
            string okStr = btnOkStr == null ? null : $"latest_btn_ok";
            var ModalMessageDataDisclaimer = new ModalMessageData
            {
                Title = title,
                Message = $"<size=70%>{msg}</size>",
                LocaliseTitle = LocaliseOption.NotLocalised,
                LocaliseMessage = LocaliseOption.NotLocalised,
                ModalType = type,
                OkButtonType = btnType,
                OnCloseButtonPressed = act,
                OkTextOverrideId = okStr,
                MessageTextAlignment = al,
                AcceptWaitObservable = acceptWaitObs,
                Priority = PopupMessagePriority.Default,

            };

            PopupManager.Instance.Show(PopupInteractionType.Error, ModalMessageDataDisclaimer);
            if (doSfx)
                AudioManager.PlayOneShot(AudioManager.EventMasterData.GenericPopUpAppears);
        }

        public static Sprite SetSpriteFromFile(string path, int Width, int Height)
        {
            byte[] ImageAsByte = File.ReadAllBytes(path);
            Texture2D Texture = new(Width, Height, TextureFormat.RGBA32, false);
            if (Texture.LoadImage(ImageAsByte))
            {
                Texture.filterMode = FilterMode.Point;
                return Sprite.Create(Texture, new Rect(0.0f, 0.0f, Texture.width, Texture.height), new Vector2(0.5f, 0.5f));
            }
            return null;
        }

        public static Sprite SetSpriteFromBytes(byte[] bytes, int Width, int Height)
        {
            Texture2D Texture = new(Width, Height, TextureFormat.RGBA32, false);
            if (Texture.LoadImage(bytes))
            {
                Texture.filterMode = FilterMode.Point;
                return Sprite.Create(Texture, new Rect(0.0f, 0.0f, Texture.width, Texture.height), new Vector2(0.5f, 0.5f));
            }
            return null;
        }

        public static void AddCMSString(string key, string value)
        {
            if (CMSLoader.Instance._localisedStrings.ContainsString(key))
                CMSLoader.Instance._localisedStrings._localisedStrings.Remove(key);
            CMSLoader.Instance._localisedStrings._localisedStrings.Add(key, value);
        }

        public static GameObject GetChild(GameObject Parent, string Name)
        {
            foreach (Transform Transform in Parent.GetComponentsInChildren<Transform>(true))
                if (Transform.name == Name) return Transform.gameObject;
            return null;
        }

        public static string CleanStr(string strIN, bool rpcFormat = false)
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

        public static long CalculateDirSize(string path)
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

        public static string CalculateSizeString(long size)
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

        public static void XorByteArray(ref byte[] data, byte[] key)
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

        public static bool BytesCheck(byte[] arr1, byte[] arr2)
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

        public static T GetItem<T>(string id) where T : ScriptableObject
        {
            foreach (T option in Resources.FindObjectsOfTypeAll<T>())
            {
                if (option.name == id)
                    return option;
            }
            return null;
        }

        public static string GetItemName(string id)
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

        public static byte[] GetFileInZip(byte[] zipContent, string file)
        {
            using var zipStream = new MemoryStream(zipContent);
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
            var localeFile = zip.Entries.FirstOrDefault(e => e.Name == file);
            byte[] loadedBytes = new BinaryReader(localeFile.Open()).ReadBytes((int)localeFile.Length);
            return loadedBytes;
        }

        public static void CreateNotification(string title, string msg, string headerCol = null, float durination = -1, Action onComplete = null)
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
