using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FGTools.Content.Attributes;

namespace FGTools.Content.ContentImpl
{
    [FGTGroup("themes_data")]
    public class FGTThemesData : Dictionary<string, ThemeData>
    {
    }

    public class ThemeData : FGTContent
    {
        [FGTField("folder_name")]
        public string FolderName { get; set; }
        [FGTField("display_name")]
        public string DisplayName { get; set; }
        [FGTField("download_url")]
        public string DownloadURL { get; set; }
        [FGTField("credit")]
        public string Credit { get; set; }
        [FGTField("is3d")]
        public bool Is3D { get; set; }
    }
}