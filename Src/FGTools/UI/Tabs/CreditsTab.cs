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
            Text bottomLine = UIFactory.CreateLabel(ControlledObject, "creditsInfo_2", $"{Launcher.DisplayName} V{Launcher.BuildInfo.UI_Version} {Description[Description.IndexOf("by")..]}", TextAnchor.LowerCenter, default, true, 14);
            UIFactory.SetLayoutElement(bottomLine.gameObject, minHeight: 5);
        }

        internal override void Refresh()
        {
        }
    }
}
