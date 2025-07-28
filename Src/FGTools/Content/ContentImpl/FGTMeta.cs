using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FGTools.Content.Attributes;

namespace FGTools.Content.ContentImpl
{
    [FGTGroup("meta")]
    public class FGTMeta
    {
        [FGTField("fgt_version")]
        public string FgtVersion { get; set; }

        [FGTField("discord_url")]
        public string DiscordUrl { get; set; }

        [FGTField("content_version")]
        public string ContentVersion { get; set; }
    }
}
