using UnityEngine.UI;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;

namespace FGTools.Internal.Extensions
{
    public static class FLZ_UIExtensions
    {
        public static void InsertDefaultOption(object target)
        {
            var defO = LocalizedStr("dropdown_placeholder");

            if (target is Dropdown drop)
            {
                if (drop.options.Count == 0 || drop.options[0].text != defO)
                    drop.options.Insert(0, new(defO));
            }

            if (target is Il2CppSystem.Collections.Generic.List<string> list)
            {
                if (list.Count == 0 || list[0] != defO)
                    list.Insert(0, defO);
            }
        }

        public static bool ForceHideDropdown(Dropdown dropdown)
        {
            var list = GetChild(dropdown.gameObject, "Dropdown List");
            if (list != null)
            {
                UnityEngine.Object.Destroy(list);
                return true;
            }

            return false;
        }

        public static bool IsValidString(string str) => !string.IsNullOrEmpty(str) && !str.Equals(LocalizedStr("dropdown_placeholder"));
    }
}
