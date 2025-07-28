using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FGTools.Content.Attributes;

namespace FGTools.Content.ContentImpl
{
    [FGTGroup("country_codes")]
    public class FGTLangCodes : List<FGTLangCode>
    {
        public string ReturnLang(string code) => Find(x => x.Code == code).Country;
    }

    public class FGTLangCode
    {
        [FGTField("country")]
        public string Country { get; set; }

        [FGTField("code")]
        public string Code { get; set; }
    }
}
