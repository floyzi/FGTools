using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FGTools.Content.Attributes;

namespace FGTools.Content.ContentImpl
{
    [FGTGroup("changelog")]
    public class FGTChangelogData : Dictionary<string, ChangelogEntry>
    {
    }

    public class ChangelogEntry : FGTContent
    {
        [FGTField("data")]
        public List<string> Data { get; set; }
    }
}
