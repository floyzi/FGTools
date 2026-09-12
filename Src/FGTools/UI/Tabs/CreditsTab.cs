using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.UI.Tabs.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using static FGTools.UI.FGToolsUI;
using static FGTools.Services.LocalizationService;

namespace FGTools.UI.Tabs
{
    internal class CreditsTab : UITab
    {
        public CreditsTab() : base(Tab.Credits)
        {

        }

        internal override string TabName => "gui_credits";
        internal override string TabTitle => "gui_credits";

        internal override void Destroy()
        {

        }

        internal override void Draw(GameObject root)
        {
            ControlledObject = UIFactory.CreateVerticalGroup(root, $"Tab_{Tab}", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(ControlledObject, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);
            GameObject scrollview = UIFactory.CreateScrollView(ControlledObject, "creditsGUI", out _, out _, new(0.1f, 0.1f, 0.1f));
            UIFactory.SetLayoutElement(scrollview, preferredHeight: 310, flexibleHeight: 9999, flexibleWidth: 9999);

            var sb = new StringBuilder();

            foreach (var credit in OnlineCheck.FGTContent.Credits)
            {
                sb.AppendLine($"<b>{LocalizedStr(credit.Value.Id).ToUpper()}</b>\n");

                foreach (var actualCredit in credit.Value.Credits)
                {
                    if (actualCredit.Perfom != string.Empty)
                        sb.AppendLine($"{actualCredit.Subject} - {actualCredit.Perfom}");
                    else
                        sb.AppendLine($"{actualCredit.Subject}");
                }

                sb.AppendLine();
            }

            Text credits = UIFactory.CreateLabel(ControlledObject, "creditsInfo", $"{sb.ToString().Trim()}", TextAnchor.LowerCenter, default, true, 14);
            credits.transform.parent = scrollview.GetComponent<ScrollRect>().content;

            Text bottomLine = UIFactory.CreateLabel(ControlledObject, "creditsInfo_2", $"{Launcher.DisplayName} V{FGToolsBuildDetails.Version} {FGToolsBuildDetails.Description[FGToolsBuildDetails.Description.IndexOf("by")..]}", TextAnchor.LowerCenter, default, true, 14);
            UIFactory.SetLayoutElement(bottomLine.gameObject, minHeight: 5);

            GameObject buttons = UIFactory.CreateHorizontalGroup(ControlledObject, "buttons", true, false, true, true, 2, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
            UIFactory.SetLayoutElement(buttons.gameObject, minHeight: 25);

            var ghBtn = UIFactory.CreateButton(buttons, "ghBtn", $"{LocalizedStr("gui_github_btn")}");
            UIFactory.SetLayoutElement(ghBtn.Component.gameObject, flexibleWidth: 9999, minHeight: 25, flexibleHeight: 0);
            ghBtn.OnClick += () => {
                if (string.IsNullOrEmpty(OnlineCheck.FGTContent?.Meta?.GithubUrl)) return;
                Application.OpenURL(OnlineCheck.FGTContent.Meta.GithubUrl); 
            };

            var discordBtn = UIFactory.CreateButton(buttons, "discordBtn", $"{LocalizedStr("gui_discord_btn")}");
            UIFactory.SetLayoutElement(discordBtn.Component.gameObject, flexibleWidth: 9999, minHeight: 25, flexibleHeight: 0);
            discordBtn.OnClick += () => {
                if (string.IsNullOrEmpty(OnlineCheck.FGTContent?.Meta?.DiscordUrl)) return;
                Application.OpenURL(OnlineCheck.FGTContent.Meta.DiscordUrl); 
            };
        }

        internal override void Refresh()
        {
        }
    }
}
