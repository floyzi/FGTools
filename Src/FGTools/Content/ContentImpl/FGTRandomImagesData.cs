using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FGTools.Content.Attributes;

namespace FGTools.Content.ContentImpl
{
    [FGTGroup("random_images")]
    public class FGTRandomImages
    {
        [FGTField("enabled")]
        public bool Enabled { get; set; }

        [FGTField("legacy_format")]
        public bool LegacyFormat { get; set; }

        [FGTField("url")]
        public string Url { get; set; }

        [FGTField("total_images")]
        public int TotalImages { get; set; }

        [FGTField("banned_images")]
        public List<int> BannedImages { get; set; }

        [FGTField("fallback")]
        public string Fallback { get; set; }
    }
}
