using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static FGTools.Internal.Extensions.FLZ_Extensions;
namespace FGTools.Services.Logic
{
    public class FGTServiceManager
    {
        private static FGTServiceManager _instance;
        readonly List<IFGTService> Services = [];
        bool errorCatch = false;
        string latestError = "";
        float timeSinceLastErrror = 0;
        public static FGTServiceManager Instance
        {
            get
            {
                _instance ??= new FGTServiceManager();
                return _instance;
            }
        }

        public void RegisterServices()
        {
            Services.Add(new DebugDisplayService());
            Services.Add(new LocalizationService());
            Services.Add(new EventService());
            Services.Add(new OnlineCheckService());
            Services.Add(new SpeedrunService());
            Services.Add(new StatisticsService());
            Services.Add(new DiscordRPCService());
            Services.Add(new CosmeticsService());
            Services.Add(new MenuThemeService());
            Services.Add(new RoundLoaderService());
            Services.Add(new RoundOptionsService());
            Services.Add(new FGC_AutosaveService());
            Services.Add(new MediaService());
            Services.Add(new PresetsService());
            Services.Add(new FGC_LocalSavesService());
            Services.Add(new ShowLoaderService());
            Services.Add(new ControllersDataService());
            Services.Add(new LocalServerService());

#if INCLUDE_FGC_CUSTOM_COLORS
            services.Add(new FGC_CustomColorService());
#endif

#if INCLUDE_BUNDLELOADER
            services.Add(new BundleLoaderService());
#endif

            foreach (var service in Services)
            {
                try
                {
                    FGTLog(LogLevel.Info, base.GetType(), $"Service \"{service.GetType().Name}\" is being registred");
                    service.RegisterService();
                }
                catch (Exception ex)
                {
                    errorCatch = true;
                    timeSinceLastErrror = 0;
                    FGTLog(LogLevel.Error, base.GetType(), $"Failed to register service \"{service.GetType().Name}\"! {ex.Message} | {ex.StackTrace}");
                    SetErrorString(ex);
                }
            }
        }

        public string ReturnDebugInfo()
        {

            return $"Services total: {Services.Count} | AtLeastOneError: {errorCatch}" +
                $"\nLatest service error: {latestError}" +
                $"\nTimeSinceLastError: {timeSinceLastErrror}";
        }

        public void Update()
        {
            if (errorCatch)
                timeSinceLastErrror += Time.unscaledDeltaTime;
            try
            {
                foreach (var service in Services)
                    service.UpdateService();
            }
            catch (Exception e) { timeSinceLastErrror = 0; SetErrorString(e); errorCatch = true; }
        }

        public void DrawGUI()
        {
            try
            {
                foreach (var service in Services)
                    service.DrawGUI();
            }
            catch (Exception e) { timeSinceLastErrror = 0; SetErrorString(e); errorCatch = true; }
        }

        void SetErrorString(Exception ex)
        {
            latestError = $"\nMessage: {ex.Message}" +
                $"\nSource: {ex.Source}" +
                $"\nStackTrace: {ex.StackTrace}";
        }

        internal T GetService<T>() where T : class, IFGTService
        {
            return Services.FirstOrDefault(s => s.GetType() == typeof(T)) as T;
        }

        public void OnGUIRefresh()
        {
            foreach ( var service in Services)
            {
                if (service is IFGTGUIHelper helper)
                {
                    helper.RefreshUI();
                }
            }
        }
    }
}
