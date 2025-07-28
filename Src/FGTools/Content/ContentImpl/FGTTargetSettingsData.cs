using System.Collections.Generic;
using System.Text.Json;
using FGTools.Content.Attributes;
using Il2CppSystem.Net;

namespace FGTools.Content.ContentImpl
{
    [FGTGroup("targets")]
    public class FGTTargetSettingsData : List<FGTTargetSetting>
    {
        public bool GetBool(string name, bool defaultValue)
        {
            var res = Find(x => x.Name == name);

            if (res != null && res.State is JsonElement element)
                return element.GetBoolean();

            return defaultValue;

        }
    }

    public class FGTTargetSetting
    {
        [FGTField("name")]
        public string Name { get; set; }

        [FGTField("state")]
        public object State { get; set; }
    }
}
