using FGTools.States.Logic;

namespace FGTools.Services.Logic
{
    internal interface IFGTService
    {
        void RegisterService();
        void UpdateService();
        void DrawGUI();
    }

    internal abstract class FGTService : FGTBase, IFGTService
    {
        public abstract void RegisterService();
        public abstract void UpdateService();
        public abstract void DrawGUI();
    }

    internal interface IFGTGUIHelper
    {
        void SetUIReferences(object[] data);
        void OnUIDestroy();
        void OnUICreated();
        void RefreshUI();
    }
}
