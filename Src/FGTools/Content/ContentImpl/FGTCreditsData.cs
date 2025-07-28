using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FGTools.Content.Attributes;

namespace FGTools.Content.ContentImpl
{
    [FGTGroup("credits")]
    public class FGTCreditsData : Dictionary<string, FGTCreditHolder>
    {

    }

    public class FGTCreditHolder : FGTContent
    {
        [FGTField("content")]
        public List<FGTCredit> Credits { get; set; }
    }

    public class FGTCredit
    {
        [FGTField("subject")]
        public string Subject { get; set; }

        [FGTField("perfom")]
        public string Perfom { get; set; }
    }

}
