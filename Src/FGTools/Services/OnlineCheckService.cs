using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Catapult.Network.Gateway;
using Catapult.Network.Http;
using Events;
using FG.Common.CMS;
using FGClient;
using FGClient.CatapultServices;
using FGClient.UI;
using FGClient.UI.Core;
using FGTools.Content;
using FGTools.Content.ContentImpl;
using FGTools.Internal.Extensions;
using FGTools.Services.Logic;
using FGTools.States;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Localisation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Localization.SmartFormat.Core.Output;
using UnityEngine.Networking;
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static Il2CppMono.Net.Security.MobileAuthenticatedStream;
namespace FGTools.Services
{
    internal class OnlineCheckService : FGTService
    {
        internal class ExploreCodesSave
        {
            public string Version { get; set; }
            public JsonElement ExploreContent { get; set; }
        }

        public static string Error = "";
        public static string LastVer = Launcher.BuildInfo.UI_Version;
        public Dictionary<string, string> ExploreCodes = [];

        LoadingScreenManager LoadingManager;
        Coroutine DownloadCoroutine;
        UnityWebRequest Request;
        UnityEngine.UI.Slider ProgressBar;
        readonly HashSet<Newsfeed.Article> PendingArticles = [];
        static int ChecksGoal { get { return 3; } }
        int Checks = 0;
        float СheckLength = 0;
        bool CheckCompleteAtLeastOnce = false;
        bool CheckInProgress;
        Action<Newsfeed.UpdatedEvent> _newsfeedUpdate;
        public string ChecksDisplay = string.Empty;
        internal FGTContentData FGTContent;

        readonly string[] KnownMirrors = [
            "floyzi.github.io/FGTools/",
            "floyzi-page.netlify.app/public/FGTools/",
            "floyzi-gitlab-io.vercel.app/FGTools/",
            "page.floyzi.workers.dev/FGTools/",
            "cdn.floyzi.ru/minimal-content/FGTools/"
        ];
        internal string UsedMirror = string.Empty;

        public override void RegisterService()
        {
            _newsfeedUpdate = new Action<Newsfeed.UpdatedEvent>(PushNewsfeeds);
            Broadcaster.Instance.Register<Newsfeed.UpdatedEvent>(_newsfeedUpdate);
        }

        public string ReturnDebugInfo()
        {
            var output = new StringBuilder();

            output.AppendLine($"Is сontent loaded: {FGTContent != null && FGTContent.Config != null}");
            output.AppendLine($"Checks goal vs done: {ChecksGoal} | {Checks}");

            if (FGTContent?.Config != null)
            {
                output.AppendLine($"Content Version: {FGTContent.ContentVersion}");
                output.AppendLine($"Latest ver: {FGTContent.Meta.FgtVersion}");
                output.AppendLine($"Target ver: {FGTContent.Config.TargetFgVersions[0]} ({FGTContent.Config.TargetFgVersions.Count()})");
            }

            if (FGTContent?.ThemeData != null)
                output.AppendLine($"WebThemes: {FGTContent.ThemeData.Count}");

            if (FGTContent?.LocaleConfig != null)
                output.AppendLine($"Translators: {FGTContent.LocaleConfig.Count}");

            if (ExploreCodes != null)
                output.AppendLine($"ExploreCodes: {ExploreCodes.Count}");

            if (FGTContent?.Newsfeeds != null)
                output.AppendLine($"Newsfeeds: {FGTContent.Newsfeeds.Count}");

            output.AppendLine($"UsedMirror: {UsedMirror}");
            output.AppendLine($"Source: {DownloadSource}");
            output.AppendLine($"NextExploreDownload: {FGTServiceManager.GetService<EventService>().ReturnScheduledEventValue("ExploreDownloadSchedule")}");
            output.AppendLine($"CheckLength: {СheckLength}");
            output.AppendLine($"CheckInProgress: {CheckInProgress}");

            if (Error != string.Empty)
                output.AppendLine(Error);

            return output.ToString();
        }

        public string ReturnChecksGoal() => $"{Checks} / {ChecksGoal}";

        public void Run()
        {
            //if (!offlineMode.Value)
            DisplayNewLoadingScreen();
            //else
            //    FinishLogin();
            CheckInProgress = true;
        }

        void ResetAll()
        {
            Checks = 0;
            ExploreCodes = null;
            CheckInProgress = false;
        }

        void DisplayNewLoadingScreen()
        {
            FGTLog(LogLevel.Info, base.GetType(), $"New check started at {DateTime.Now}. Source: {DownloadSource}");
            LoadingManager = Resources.FindObjectsOfTypeAll<LoadingScreenManager>().FirstOrDefault();
            LoadingManager.HideScreen(new(true));
            LoadingManager.ShowScreen(new(true));
            ProgressBar = LoadingManager._loadingScreen.gameObject.GetComponentInChildren<UnityEngine.UI.Slider>();
            DownloadCoroutine = CoroutineRunner.Instance.StartCoroutine(StartDownloading(DownloadSource, Launcher.BuildInfo.Config.ToLower() != "prod").WrapToIl2Cpp());
        }

        public void DownloadNewLang(string newLang, Action after)
        {
            if (LoadingManager._loadingScreen != null)
                LoadingManager.HideScreen(new(true));
            LoadingManager.ShowScreen(new(false));
            ProgressBar = LoadingManager._loadingScreen.gameObject.GetComponentInChildren<UnityEngine.UI.Slider>();
            CoroutineRunner.Instance.StartCoroutine(DownloadNewLangIEnum(newLang, after).WrapToIl2Cpp());
        }

        IEnumerator DownloadNewLangIEnum(string newLang, Action after)
        {
            Request = UnityWebRequest.Get($"{UsedMirror}localization/{newLang}/{newLang}.zip");
            UnityWebRequestAsyncOperation langOp = Request.SendWebRequest();

            yield return DownloadProgress(langOp, LocalizedStr("gui_download_progress_lang", null, true));

            if (Request.result != UnityWebRequest.Result.Success)
            {
                FGTLog(LogLevel.Warning, base.GetType(), $"Unable to fetch localization ({newLang}) from mirror [{UsedMirror}] {Request.error}");
                Error += $" (locale ({UsedMirror}) {Request.error})";
                ReadLocale(newLang, LocaleParseStrategy.Generic);
            }
            else
            {
                ReadLocale(newLang, LocaleParseStrategy.NewLang);
            }

            after.Invoke(); 
            if (LoadingManager._loadingScreen != null)
                LoadingManager.HideScreen(new(false));
        }

        enum LocaleParseStrategy
        {
            Generic,
            NewLang
        }

        void ReadLocale(string langCode, LocaleParseStrategy parseStrategy)
        {
            var targetDir = Path.Combine(Launcher.LocalizationDir, langCode);
            var targetPath = Path.Combine(targetDir, "locale.json");
            byte[] newBytes = null;

            byte[] savedBytes;
            if (parseStrategy == LocaleParseStrategy.Generic)
                savedBytes = File.ReadAllBytes(LocalizationService.SelectedLocalizeFolder + "locale.json");
            else
                savedBytes = File.Exists(targetPath) ? File.ReadAllBytes(targetPath) : [];

            newBytes = GetFileInZip(Request?.downloadHandler?.data, "locale.json");

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            if (newBytes != null && newBytes.Length > 0)
            {
                bool refresh = FGTContent.Config != null && FGTContent.Config.AlwaysRefreshLocale;
                if (!BytesCheck(savedBytes, newBytes) || refresh)
                {
                    FGTLog(LogLevel.Warning, base.GetType(), $"Updating localization ({langCode}) - forced: {refresh}");
                    File.WriteAllBytes(targetPath, newBytes);
                }

                File.WriteAllBytes(SelectedLocalizeFolder + $"BACKUP_locale.json", savedBytes);

                if (UseBackupLocale.Value)
                {
                    FGTLog(LogLevel.Warning, base.GetType(), $"Using backup localization!!");
                    targetPath = SelectedLocalizeFolder + $"BACKUP_locale.json";
                }

                if (parseStrategy == LocaleParseStrategy.Generic)
                    FGTServiceManager.GetService<LocalizationService>().SetupLocalization(targetPath);
            }
            else
            {
                FGTLog(LogLevel.Error, GetType(), "Failed to get localization bytes, forcing load of fallback localization...");
                targetPath = SelectedLocalizeFolder + $"BACKUP_locale.json";
                if (!File.Exists(targetPath))
                {
                    FGTLog(LogLevel.Error, GetType(), "Unable to find fallback localization!!");
                    return;
                }
                FGTServiceManager.GetService<LocalizationService>().SetupLocalization(targetPath);
            }
        }

        void CreateNewNewsfeed(FGTNewsfeed newsfeed)
        {
            if (!FGTTargetSettings.FGTNewsfeed)
                return;

            if (newsfeed.VisibilityType == NewsfeedVisibleType.OnlyDev && Launcher.BuildInfo.Config != "DEV")
                return;

            if (newsfeed.VisibilityType == NewsfeedVisibleType.OnlyClosedBetaTesters && Launcher.BuildInfo.Config != "ClosedBeta")
                return;

            if (newsfeed.TargetFGTVersions != null && !newsfeed.TargetFGTVersions.Contains(Launcher.BuildInfo.UI_Version))
                return;

            Il2CppReferenceArray<DescriptionParameter> EndsAtDescription = new(1);
            EndsAtDescription[0] = new()
            {
                DescriptionParameterType = DescriptionParameter.ParameterType.Localised,
                LocalisedString = new() { _text = newsfeed.EndsAtDescription },
                NumericValue = 0

            };

            Il2CppReferenceArray<DescriptionParameter> StartsAtDescription = new(1);
            StartsAtDescription[0] = new()
            {
                DescriptionParameterType = DescriptionParameter.ParameterType.Localised,
                LocalisedString = new() { _text = newsfeed.StartsAtDescription },
                NumericValue = 0

            };

            Il2CppReferenceArray<NewsfeedArticlePage> Pages = new(1);

            string locale = LocalisationUtils.GetLocaleDevicePrefOverride();
            LocalisedString startLocalised = null;
            if (newsfeed.StartDate.HasValue)
            {
                string startDate = newsfeed.StartDate.Value.ToString("dd MMMM", CultureInfo.GetCultureInfo(locale));
                startLocalised = new() { Text = null };
            }

            LocalisedString endLocalised = null;
            if (newsfeed.EndDate.HasValue)
            {
                string endDate = newsfeed.EndDate.Value.ToString("dd MMMM", CultureInfo.GetCultureInfo(locale));
                endLocalised = new() { Text = endDate };
            }

            LocalisedString headerLocalised = null;
            string header = null;
            if (newsfeed.Header != null)
            {
                if (!newsfeed.UseFGTLocalizedStr)
                    header = newsfeed.Header;
                else
                    header = LocalizedStr(newsfeed.Header);

                headerLocalised = new() { Text = header };
            }

            LocalisedString titleLocalised = null;
            if (newsfeed.Title != null)
            {
                string title;
                if (!newsfeed.UseFGTLocalizedStr)
                    title = newsfeed.Title;
                else
                    title = LocalizedStr(newsfeed.Title);

                titleLocalised = new() { Text = title };
            }

            LocalisedString msgLocalised = null;
            string msg = null;
            if (newsfeed.Message != null)
            {
                if (!newsfeed.UseFGTLocalizedStr)
                    msg = newsfeed.Message;
                else
                    msg = LocalizedStr(newsfeed.Message);

                msgLocalised = new() { Text = msg };
            }

            var neeNewsFeedPage = new NewsfeedArticlePage()
            {
                EndsAtDescription = EndsAtDescription,
                StartsAtDescription = StartsAtDescription,
                EndsAt = endLocalised,
                StartsAt = startLocalised,
                Header = headerLocalised,
                Title = titleLocalised,
                Message = msgLocalised,
                Image = newsfeed.ImagePath,
                DeeplinkData = new()
                {

                }
            };

            Pages[0] = neeNewsFeedPage;

            Il2CppSystem.DateTime StartDate = default;
            Il2CppSystem.DateTime EndDate = default;

            if (newsfeed.StartDate.HasValue)
                StartDate = new(newsfeed.StartDate.Value.Year, newsfeed.StartDate.Value.Month, newsfeed.StartDate.Value.Day, newsfeed.StartDate.Value.Hour, newsfeed.StartDate.Value.Minute, newsfeed.StartDate.Value.Second, newsfeed.StartDate.Value.Millisecond, Il2CppSystem.DateTimeKind.Utc);

            if (newsfeed.EndDate.HasValue)
                EndDate = new(newsfeed.EndDate.Value.Year, newsfeed.EndDate.Value.Month, newsfeed.EndDate.Value.Day, newsfeed.EndDate.Value.Hour, newsfeed.EndDate.Value.Minute, newsfeed.EndDate.Value.Second, newsfeed.EndDate.Value.Millisecond, Il2CppSystem.DateTimeKind.Utc);


            var newNewsfeed = new NewsfeedArticleSchema()
            {
                StartsAt = StartDate,
                EndsAt = EndDate,
                Id = newsfeed.Id,
                Pages = Pages,
                Priority = newsfeed.Priority,
            };
            if (!CMSLoader.Instance.CMSData.Newsfeed.ContainsKey(newsfeed.Id))
                CMSLoader.Instance.CMSData.Newsfeed.Add(newsfeed.Id, newNewsfeed);

            Newsfeed.Article a = new()
            {
                Id = newNewsfeed.Id,
                StartsAt = newNewsfeed.StartsAt,
                Pages = Pages,
                Priority = newsfeed.Priority,
            };

            DlcImagesSchema dlcImage = new()
            {
                Id = $"IMG_" + newNewsfeed.Id,
                DlcItem = new()
                {
                    Base = "images/newsfeed/",
                    Path = $"{newsfeed.ImagePath}.{"png"}"
                }
            };

            if (!CMSLoader.Instance.Dlc._dlcImagesData.ContainsKey(newsfeed.ImagePath))
                CMSLoader.Instance.Dlc._dlcImagesData.Add(newsfeed.ImagePath, dlcImage);


            if ((!newsfeed.StartDate.HasValue || DateTime.UtcNow >= newsfeed.StartDate.Value) && (!newsfeed.EndDate.HasValue || DateTime.UtcNow <= newsfeed.EndDate.Value))
            {
                PendingArticles.Add(a);
                FGTLog(LogLevel.Info, base.GetType(), $"Created new newsfeed with id {newsfeed.Id}");
            }
            else
            {
                FGTLog(LogLevel.Warning, base.GetType(), $"Newsfeed with id {newsfeed.Id} expired");
                return;
            }
        }

        private IEnumerator DownloadContent(string url, string message, Dictionary<string, string> headers, Action<string> onSuccess, Action<string> onError, string specialMsg = null)
        {
            ResetRequest();

            Request = UnityWebRequest.Get(url);
            if (headers != null)
            {
                foreach (var header in headers)
                    Request.SetRequestHeader(header.Key, header.Value);
            }
            UnityWebRequestAsyncOperation op = Request.SendWebRequest();

            var id = $"latest_download_msg_{Guid.NewGuid()}";
            var msg = LocalizedStr(message).ToUpper();
            FLZ_Extensions.AddCMSString(id, msg.EndsWith("...") ? msg : $"{msg}...");
            LoadingManager._loadingScreen.UpdateDisplay(id, string.IsNullOrEmpty(message), false, specialMsg, true, true, 0, true, 0, 0);

            yield return DownloadProgress(op, LocalizedStr(message), specialMsg);

            if (Request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(Request.error);
                yield break;
            }

            try
            {
                onSuccess?.Invoke(Request.downloadHandler.text);
                Checks++;
            }
            catch (Exception ex)
            {
                ResetAll();
                DoModal(new(LocalizedStr("content_err_title"), LocalizedStr("content_err_desc", [ex.Message]), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, new Action<bool>(wasOk =>
                {
                    if (!wasOk)
                        Application.Quit();
                    else
                        Run();
                }), okStrOverride: LocalizedStr("content_err_ok")));

                CoroutineRunner.End(DownloadCoroutine);
                DownloadCoroutine = null;

                onError?.Invoke(ex.ToString());

            }

        }

        IEnumerator StartDownloading(string ver, bool trackChecksInfo)
        {
            ResetAll();
            DoModal(new(LocalizedStr("gui_downloading_title"), LocalizedStr("gui_downloading_desc"), FGClient.UI.UIModalMessage.ModalType.MT_NO_BUTTONS, FGClient.UI.UIModalMessage.OKButtonType.Default, priority: 9999));

            for (int i = 0; i < KnownMirrors.Length; i++)
            {
                var url = KnownMirrors[i];
                var fullUrl = $"{url}contentV2/{ver}.json";
                if (!url.StartsWith("http"))
                    fullUrl = $"https://{fullUrl}";

                var testReq = UnityWebRequest.Get(fullUrl);
                testReq.timeout = 5;

                FGTLog(LogLevel.Info, GetType(), $"Testing mirror [{url}]...");

                yield return testReq.SendWebRequest();

                if (testReq.result != UnityWebRequest.Result.Success)
                {
                    FGTLog(LogLevel.Warning, GetType(), $"Mirror [{url}] test failed [{testReq.error}]");
                    continue;
                }

                FGTLog(LogLevel.Message, GetType(), $"Selecting mirror [{url}] !");
                UsedMirror = url;
                if (!url.StartsWith("http") || !url.StartsWith("https"))
                    UsedMirror = "https://" + UsedMirror;
                break;
            }

            if (string.IsNullOrEmpty(UsedMirror))
            {
                ResetAll();
                DoModal(new(LocalizedStr("no_mirror_err_title"), LocalizedStr("no_mirror_err_desc"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, new Action<bool>(wasOk =>
                {
                    if (!wasOk)
                        Application.Quit();
                    else
                        Run();
                }), okStrOverride: LocalizedStr("content_err_ok")));
                CoroutineRunner.End(DownloadCoroutine);
                DownloadCoroutine = null;

                yield break;
            }

            //content
#if !LOCAL_CONTENT
            yield return DownloadContent($"{UsedMirror}contentV2/{ver}.json", "gui_download_progress_content", null, result =>
            {
                FGTContent = new(Request.downloadHandler.text);
            }, errorMsg =>
            {
                throw new Exception($"Content download failed - {errorMsg}");
            });
#else
            FGTLog(LogLevel.Warning, GetType(), "BUILD USING LOCAL CONTENT FILE!");

            if (!File.Exists(Plugin.JsonTesting))
                throw new InvalidOperationException($"Build should use local content file but no content file were found at path \"{Plugin.JsonTesting}\"");

            FGTContent = new(File.ReadAllText(Plugin.JsonTesting));
            Checks++;
#endif

            //lang
            yield return DownloadContent($"{UsedMirror}localization/{LangFileName.Value}/{LangFileName.Value}.zip", "gui_download_progress_lang", null, result =>
            {
                ReadLocale(LangFileName.Value, LocaleParseStrategy.Generic);
            }, errorMsg =>
            {
                FGTLog(LogLevel.Warning, base.GetType(), $"Unable to fetch localization ({LangFileName.Value}) from server {errorMsg}");
                Error += $"\n(locale {errorMsg})";
                ReadLocale(LangFileName.Value, LocaleParseStrategy.Generic);
            });

            //explore
            if (!File.Exists(Launcher.ExploreBackupV2))
                FGTServiceManager.GetService<EventService>().SetEventValue("ExploreDownloadSchedule", DateTime.UtcNow);

            if (FGTServiceManager.GetService<EventService>().CheckIfEventIsExpired("ExploreDownloadSchedule"))
            {
                string pools = "";
                //not proud of this
                foreach (var a in CMSLoader.Instance.CMSData.DiscoveryQueries)
                    pools += $"id={a.Value.Id}&";

                yield return DownloadContent($"{FG_ExploreAPI}?{pools}", "gui_download_progress_explore", new()
                {
                    { "Authorization", $"Bearer {CatapultServicesManager.Instance.GatewayConfig.HttpConfig.AuthProvider.Cast<HttpAuthProvider>()._token}" }
                },
                result =>
                {
                    try
                    {
                        File.WriteAllText(Launcher.ExploreBackupV2, JsonSerializer.Serialize(ParseCodes(result)));
                        FGTServiceManager.GetService<EventService>().SetEventValue("ExploreDownloadSchedule", DateTime.UtcNow.AddDays(5));
                    }
                    catch (Exception e)
                    {
                        FGTLog(LogLevel.Warning, base.GetType(), $"Failed to parse explore rounds: {e.Message}");
                    }
                },
                errorMsg =>
                {
                    FGTLog(LogLevel.Error, base.GetType(), "Failed to fetch info about explore rounds! " + errorMsg);
                    Error += $"\n(explore {errorMsg})";
                    if (File.Exists(Launcher.ExploreBackupV2))
                    {
                        var scheme = JsonSerializer.Deserialize<ExploreCodesSave>(File.ReadAllText(Launcher.ExploreBackupV2));
                        ParseCodes(scheme.ExploreContent);
                    }
                }, LocalizedStr("gui_download_progress_explore_waiting", null, true));
            }
            else
            {
                FGTLog(LogLevel.Info, base.GetType(), $"Next explore rounds update is set to {FGTServiceManager.GetService<EventService>().ReturnScheduledEventValue("ExploreDownloadSchedule")}. Using old codes for now");
                var scheme = JsonSerializer.Deserialize<ExploreCodesSave>(File.ReadAllText(Launcher.ExploreBackupV2));
                ParseCodes(scheme.ExploreContent);
                Checks++;
            }

            if (!CheckCompleteAtLeastOnce)
                CheckCompleteAtLeastOnce = true;

            ResetRequest();
            yield return new WaitForEndOfFrame();

            if (FGTContent != null && FGTContent.Newsfeeds != null)
            {
                foreach (var newsfeed in FGTContent.Newsfeeds)
                    CreateNewNewsfeed(newsfeed);
            }

            FinishLogin();
        }

        IEnumerator DownloadProgress(UnityWebRequestAsyncOperation operation, string progress_bar_id, string special_msg = null)
        {
            while (!operation.isDone)
            {
                //this was used for progress bar before
                yield return null;
            }
        }

        ExploreCodesSave ParseCodes(object json)
        {
            ExploreCodes = [];

            JsonNode doc = null;

            if (json is string jsonStr)
                doc = JsonNode.Parse(jsonStr);

            if (json is JsonElement element)
                doc = JsonNode.Parse(element.GetRawText());

            foreach (var item in doc as JsonArray)
            {
                if (item?["levels"] is JsonArray levels)
                {
                    foreach (var level in levels)
                    {
                        if (level?["snapshot"]?["version_metadata"] is JsonObject meta)
                        {
                            var code = level["snapshot"]?["share_code"]?.ToString();
                            var mode = meta["game_mode_id"]?.ToString();

                            if (code != null && !ExploreCodes.ContainsKey(code))
                                ExploreCodes.Add(code, mode);

                            if (level is JsonObject levelObj)
                                levelObj["token"] = "";
                        }
                    }
                }
            }

            return new()
            {
                Version = "V2",
                ExploreContent = JsonDocument.Parse(doc.ToJsonString()).RootElement
            };
        }


        void ResetRequest()
        {
            Request?.Dispose();
            Request = null;
        }

        void FinishLogin()
        {
            CheckInProgress = false;
            Resources.FindObjectsOfTypeAll<PopupManager>().FirstOrDefault().HideActivePopup();
            Resources.FindObjectsOfTypeAll<MainMenuManager>().FirstOrDefault().OnTitleScreenComplete();
            CatapultAnalyticsClient.Boot.BootCompleteEvent.TrackEvent();
            GlobalGameStateClient.Instance.BootTimeLogger?.BootComplete();

            if (CatapultGatewayConnection.Instance.TryGetAccountId(out string accountId))
                Broadcaster.Instance.Broadcast(new LoginSuccessfulEvent(accountId, TitleScreenViewModel.CameFromSplashScreen));
        }

        public bool TryBuildChangelog(string ver, out string changelog)
        {
            var builder = new StringBuilder();

            if (!FGTContent.Changelog.TryGetValue(ver, out var log))
            {
                builder.AppendLine("This version doesn't have attached changelog.");
                changelog = builder.ToString();
                return false;
            }

            foreach (var raw in log.Data)
            {
                var str = raw?.Trim();

                if (string.IsNullOrWhiteSpace(str))
                    continue;

                if (str.StartsWith("[") && str.EndsWith("]"))
                {
                    builder.AppendLine($"\n• {str.Trim('[', ']', ' ').ToUpperInvariant()}");
                }
                else
                {
                    builder.AppendLine($"  - {str}");
                }
            }

            changelog = builder.ToString();
            return true;
        }

        public override void UpdateService()
        {
            if (!CheckCompleteAtLeastOnce && CheckInProgress)
                СheckLength += Time.unscaledDeltaTime;
        }

        public override void DrawGUI()
        {
        }

        void PushNewsfeeds(Newsfeed.UpdatedEvent evt)
        {
            if (!FGTTargetSettings.FGTNewsfeed)
                return;

            foreach (var article in PendingArticles)
            {
                Newsfeed.Instance.Articles.Insert(0, article);
            }
            PendingArticles.Clear();
        }
    }
}
