using FGTools.States.Logic;

namespace FGTools.Services.Logic
{
    internal interface IFGTService
    {
        void RegisterService();
        void UpdateService();
        void DrawGUI();
        void OnAppFocus(bool focus);
        void OnAppQuit();
    }

    internal abstract class FGTService : FGTBase, IFGTService
    {
        public abstract void RegisterService();
        public abstract void UpdateService();
        public abstract void DrawGUI();
        public abstract void OnAppFocus(bool focus);
        public abstract void OnAppQuit();
    }

    internal interface IFGTGUIHelper
    {
        void SetUIReferences(object[] data);
        void OnUIDestroy();
        void OnUICreated();
        void RefreshUI();
    }
}
