using FGTools.Services.Logic;
using FGTools.States.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static FGTools.UI.FGToolsUI;

namespace FGTools.UI.Tabs.Logic
{
    internal abstract class UITab(Tab tab) : FGTBase
    {
        internal Dictionary<GroupPolicy, Func<bool>> StatePerGroup;
        internal Tab Tab { get; } = tab;
        internal GameObject ControlledObject;
        internal Button TabButton;
        internal abstract void Draw(GameObject root);
        internal abstract void Refresh();
        internal virtual void OnStateChange(ObjectGroup group)
        {

        }

        internal virtual void Update()
        {

        }
    }

    internal abstract class UITab<TService>(Tab tab, TService serviceDependency) : UITab(tab) where TService : FGTService
    {
        internal TService Service { get; } = serviceDependency;
    }
}
