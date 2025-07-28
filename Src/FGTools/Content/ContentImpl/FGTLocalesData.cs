using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FGTools.Content.Attributes;
using static FGTools.Services.LocalizationService;
using FGTools.Services;
using FGTools.Services.Logic;

namespace FGTools.Content.ContentImpl
{
    [FGTGroup("locale_config")]
    public class FGTLocalesData : List<FGTLocaleData>
    {
        public string ReturnTranslateAuthor(string langCode)
        {
            if (string.IsNullOrEmpty(langCode))
                return string.Empty;

            var e = Find(x => x.ForLang == langCode).Author;
            if (e == null)
                e = LocalizedStr("gui_unknown");

            return e;
        }

        public DateTime ReturnLastUpdate(string langCode)
        {
            if (string.IsNullOrEmpty(langCode))
                return DateTime.MinValue;

            var e = Find(x => x.ForLang == langCode).LastUpdate;
            if (e.HasValue)
                return e.Value;
            else
                return DateTime.MinValue;
        }

        public string ParseOfficialLanguagesStr(string input)
        {
            var ogLangs = FindAll(x => x.Author == "Floyzi");
            List<string> names = new();

            foreach (var o in ogLangs)
                names.Add(FGTServiceManager.Instance.GetService<OnlineCheckService>().FGTContent.LangCodes.ReturnLang(o.ForLang));

            return string.Format(input, ogLangs.Count, string.Join(", ", names.ToArray()));
        }
    }

    public class FGTLocaleData
    {
        [FGTField("locale")]
        public string ForLang { get; set; }
        [FGTField("author")]
        public string Author { get; set; }
        [FGTField("author_url")]
        public string AuthorURL { get; set; }
        [FGTField("last_update")]
        public DateTime? LastUpdate { get; set; }
    }
}
