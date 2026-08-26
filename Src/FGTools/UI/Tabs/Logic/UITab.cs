using FGTools.Services.Logic;
using FGTools.States.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static FGTools.UI.NewGUI;

namespace FGTools.UI.Tabs.Logic
{
    internal abstract class UITab(Tab tab) : FGTBase
    {
        internal Dictionary<GroupPolicy, Func<bool>> StatePerGroup;
        protected Tab Tab { get; } = tab;
        internal GameObject ControlledObject;
        internal abstract void Draw();
    }

    internal abstract class UITab<TService>(Tab tab, TService serviceDependency) : UITab(tab) where TService : FGTService
    {
        internal TService Service { get; } = serviceDependency;
    }
}
