using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FG.Common.CMS;
using FGTools.Content.Attributes;

namespace FGTools.Content.ContentImpl
{
    [FGTGroup("newsfeed_data")]
    public class FGTNewsfeedData : List<FGTNewsfeed>
    {
    }

    public class FGTNewsfeed
    {
        [FGTField("id")]
        public string Id { get; set; }

        [FGTField("priority")]
        public int Priority { get; set; }

        [FGTField("start_date")]
        public DateTime? StartDate { get; set; }

        [FGTField("starts_at_desc")]
        public string StartsAtDescription { get; set; }

        [FGTField("end_date")]
        public DateTime? EndDate { get; set; }

        [FGTField("ends_at_desc")]
        public string EndsAtDescription { get; set; }

        [FGTField("header")]
        public string Header { get; set; }

        [FGTField("title")]
        public string Title { get; set; }

        [FGTField("message")]
        public string Message { get; set; }

        [FGTField("img_path")]
        public string ImagePath { get; set; }

        [FGTField("target_fgt_versions")]
        public List<string> TargetFGTVersions { get; set; }

        [FGTField("fgt_localization")]
        public bool UseFGTLocalizedStr { get; set; }

        [FGTField("visability_level")]
        public NewsfeedVisibleType VisibilityType { get; set; }
    }

    public enum NewsfeedVisibleType
    {
        Everyone = 0,
        OnlyBetaTesters = 1,
        OnlyClosedBetaTesters = 2,
        OnlyDev = 3,
    }
}
