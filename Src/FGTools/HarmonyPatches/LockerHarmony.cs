using FallGuys.Player.Protocol.Client.Cosmetics;
using FG.Common;
using FG.Common.CMS;
using FGClient;
using FGClient.Customiser;
using FGClient.UI;
using FGTools.Internal.Extensions;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
using static FGTools.Config.Config;
using static FGTools.Services.LocalizationService;

namespace FGTools.HarmonyPatches
{
    public class LockerHarmony : FGTBase
    {
        [HarmonyPatch(typeof(CustomiserScreenViewModel), nameof(CustomiserScreenViewModel.DoExitSubMenu)), HarmonyPostfix]
        static void DoExitSubMenu(CustomiserScreenViewModel __instance, bool keepChanges)
        {
            switch (__instance.CurrentScreen)
            {
                case CustomiserScreens.Outfits:
                    CosmeticsService.ResolveFavIds(__instance._outfitMenuViewModel);
                    break;
                case CustomiserScreens.Theatrics:
                    CosmeticsService.ResolveFavIds(__instance._theatricsMenuViewModel);
                    break;
                case CustomiserScreens.Interface:
                    CosmeticsService.ResolveFavIds(__instance._interfaceMenuViewModel);
                    break;
            }

            File.WriteAllText(Launcher.CustomFavList, JsonSerializer.Serialize(CosmeticsService.FavList));
        }

        [HarmonyPatch(typeof(CustomiserMenuViewModel), nameof(CustomiserMenuViewModel.MoveToPage)), HarmonyPrefix]
        static bool MoveToPagePref(CustomiserMenuViewModel __instance, int pageIndex)
        {
            var service = FGTServiceManager.GetService<CosmeticsService>();
            service.PreviousSection = __instance.CurrentSectionText;
            service.ResolveSections();
            return true;
        }

        [HarmonyPatch(typeof(CustomiserMenuViewModel), nameof(CustomiserMenuViewModel.MoveToPage)), HarmonyPostfix]
        static void MoveToPagePost(CustomiserMenuViewModel __instance, int pageIndex)
        {
            var service = FGTServiceManager.GetService<CosmeticsService>();
            service.CurrentSection = __instance.CurrentSectionText;
            if (service.CurrentSection == service.PreviousSection) return;
            service.ResumeSearch();
        }

        //this affects all CustomiserSectionBase<,> implementations, not only colors section
        [HarmonyPatch(typeof(CustomiserSectionBase<ColourOption, ColourSchemeDto>), nameof(CustomiserSectionBase<,>.UpdateItemInfo)), HarmonyPostfix]
        static void UpdateItemInfo(CustomiserSectionBase<ColourOption, ColourSchemeDto> __instance, IItemDefinition item)
        {
            if (ShowItemIds.Value)
                __instance._customiserMenu.SelectedText = $"{item.DisplayName}\n<size=50%>{item.ItemId}</size>";
        }
        
        //[HarmonyPatch(typeof(CustomiserMenuViewModel), nameof(CustomiserMenuViewModel.CurrentSectionText), MethodType.Setter), HarmonyPrefix]
        //static bool MoveToPagePost(CustomiserMenuViewModel __instance, ref string value)
        //{
        //    value = $"{CMSLoader.Instance._localisedStrings.GetString(value)}\n<size=50%>test</size>";
        //    return true;
        //}

        [HarmonyPatch(typeof(CustomiserScreenViewModel), nameof(CustomiserScreenViewModel.HandleConfigureRequestFailed)), HarmonyPrefix]
        static bool HandleConfigureRequestFailed(CustomiserScreenViewModel __instance, Il2CppSystem.Exception error, CustomisationSelections previousSelections, bool isEmotes)
        {
            __instance.HideSpinner();

            if (!AllCosmeticsAlert.Value)
                return false;

            FLZ_Extensions.DoModal(new(LocalizedStr("request_error_2"), LocalizedStr("request_error_save_config_2"), UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.CallToAction, new Action<bool>(wasok =>
            {
                if (wasok)
                    __instance.DoExitSubMenu(true);
            })));

            return false;
        }

        [HarmonyPatch(typeof(CustomiserScreenViewModel), nameof(CustomiserScreenViewModel.HandleFavouriteError)), HarmonyPrefix]
        static bool HandleConfigureRequestFailed(CustomiserScreenViewModel __instance, Il2CppSystem.Exception error, [DefaultParameterValue(null)] OnSendFavouritesRequest favouriteRequest)
        {
            __instance.HideSpinner();
            return false;
        }
    }
}
