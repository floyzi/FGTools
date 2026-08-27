using FGTools.Services.Logic;
using FGTools.States.Logic;
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
using static FGTools.Internal.Extensions.FLZ_Extensions;
using BepInEx.Logging;

namespace FGTools.UI.Tabs.Logic
{
    internal abstract class UITab(Tab tab) : FGTBase
    {
        internal abstract string TabName { get; }
        internal abstract string TabTitle { get; }

        internal Tab Tab { get; } = tab;
        internal Dictionary<GroupPolicy, Func<bool>> StatePerGroup;
        internal GameObject ControlledObject;
        internal Button TabButton;
        internal abstract void Draw(GameObject root);
        internal abstract void Refresh();
        internal abstract void Destroy();
        internal virtual void OnStateChange(ObjectGroup group)
        {

        }

        internal virtual void Update()
        {

        }

        protected void TryDrawUI(Func<bool> condition, GameObject group, Action onValid)
        {
            if (condition())
            {
                onValid();
                return;
            }

            FGTLog(LogLevel.Warning, GetType(), $"Refused to draw content of {group.name}, feature disabled");

            var failTitle = UIFactory.CreateLabel(group, "failTitle", LocalizedStr("gui_disabled_feature"), TextAnchor.MiddleCenter);
            UIFactory.SetLayoutElement(failTitle.gameObject, minHeight: 25, flexibleHeight: 0);
        }

    }

    internal abstract class UITab<TService>(Tab tab, TService serviceDependency) : UITab(tab) where TService : FGTService
    {
        internal TService Service { get; } = serviceDependency;
    }
}
