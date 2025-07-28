using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FGTools.Content.Attributes;

namespace FGTools.Content.ContentImpl
{
    [FGTGroup("fgt_config")]
    public class FGTConfig
    {
        [FGTField("auto_app_quit")]
        public bool AutoAppQuit { get; set; }
        [FGTField("quit_on_versions")]
        public object QuitOnVersions { get; set; }
        [FGTField("outdated_versions")]
        public List<string> OutdatedVersions { get; set; }
        [FGTField("target_fg_versions")]
        public List<string> TargetFgVersions { get; set; }
        [FGTField("auto_locale_refresh")]
        public bool AlwaysRefreshLocale { get; set; }
    }
}
