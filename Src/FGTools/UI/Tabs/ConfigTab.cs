using BepInEx.Configuration;
using FGTools.UI.ConfigManager.UI;
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
    internal class ConfigTab : UITab
    {
        public ConfigTab() : base(Tab.Config)
        {

        }

        internal class EntryInfo(CachedConfigEntry cached)
        {
            public CachedConfigEntry Cached { get; } = cached; 
            public ConfigEntryBase RefEntry;
            public bool IsHidden { get; internal set; }
            internal GameObject Content;
        }

        List<EntryInfo> _confEntries;

        void SearchConfig(string q)
        {
            q = q.ToLower();

            foreach (var entry in _confEntries)
            {
                bool val = (string.IsNullOrEmpty(q) || entry.RefEntry.Definition.Key.ToLower().Contains(q) || (entry.RefEntry.Description?.Description?.Contains(q) ?? false)) && (!entry.IsHidden);
                entry.Content.SetActive(val);
            }
        }

        internal override void Draw(GameObject root)
        {
            _confEntries = [];

            ControlledObject = UIFactory.CreateVerticalGroup(root, $"Tab_{Tab}", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(ControlledObject, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);
            var search = UIFactory.CreateInputField(ControlledObject, "configGUI", LocalizedStr("gui_search"));
            search.OnValueChanged += SearchConfig;
            UIFactory.SetLayoutElement(search.GameObject, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 25);
            GameObject content = UIFactory.CreateScrollView(ControlledObject, "configGUI", out _, out _, new(0.1f, 0.1f, 0.1f));
            UIFactory.SetLayoutElement(content, preferredHeight: 310, flexibleHeight: 9999, flexibleWidth: 9999);
            var scroll = content.GetComponent<ScrollRect>().content.gameObject;

            var dict = new Dictionary<string, List<ConfigEntryBase>>()
            {
                { "", new List<ConfigEntryBase>() }
            };

            foreach (var entry in Launcher.BepConfig.Keys)
            {
                string sec = entry.Section;
                sec ??= "";

                if (!dict.ContainsKey(sec))
                    dict.Add(sec, []);

                dict[sec].Add(Launcher.BepConfig[entry]);
            }

            foreach (var ctg in dict)
            {
                if (!string.IsNullOrEmpty(ctg.Key))
                {
                    var bg = UIFactory.CreateHorizontalGroup(scroll, "TitleBG", true, true, true, true, 0, default, new Color(0.07f, 0.07f, 0.07f));

                    var title = UIFactory.CreateLabel(bg, $"Title_{ctg.Key}", ctg.Key, TextAnchor.MiddleCenter, default, true, 17);
                    UIFactory.SetLayoutElement(title.gameObject, minHeight: 30, minWidth: 200, flexibleWidth: 9999);
                }

                foreach (var configEntry in ctg.Value)
                {
                    CachedConfigEntry cache = new(configEntry, scroll);
                    cache.Enable();

                    GameObject obj = cache.UIroot;

                    bool advanced = false;

                    if (!advanced)
                    {
                        object[] tags = configEntry.Description?.Tags;
                        if (tags != null && tags.Any())
                        {
                            if (tags.Any(it => it is string s && s == "Advanced"))
                            {
                                advanced = true;
                            }
                            else if (tags.FirstOrDefault(it => it.GetType().Name == "ConfigurationManagerAttributes") is object attributes)
                            {
                                advanced = (bool?)attributes.GetType().GetField("IsAdvanced")?.GetValue(attributes) == true;
                            }
                        }
                    }

                    _confEntries.Add(new EntryInfo(cache)
                    {
                        RefEntry = configEntry,
                        Content = obj,
                        IsHidden = advanced
                    });
                }
            }

            content.SetActive(true);
        }

        internal override void Refresh()
        {

        }
    }
}
