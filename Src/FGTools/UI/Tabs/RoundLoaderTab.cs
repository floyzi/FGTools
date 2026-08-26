using FGTools.Services;
using FGTools.States.Logic;
using FGTools.UI.Tabs.Logic;
using System;

namespace FGTools.UI.Tabs
{
    internal class RoundLoaderTab : UITab<RoundLoaderService>
    {
        public RoundLoaderTab() : base(NewGUI.Tab.RoundLoader, FGTServiceManager.GetService<RoundLoaderService>())
        {
        }


        internal override void Draw()
        {
            throw new NotImplementedException();
        }
    }
}
