using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FGTools.Content.Attributes;

namespace FGTools.Content.ContentImpl
{
    [FGTGroup("user_shows_data")]
    public class FGTUserShowsData : List<UserShow>
    {
    }

    public class UserShow
    {
        [FGTField("show_author")]
        public string ShowAuthor { get; set; }
        [FGTField("show_name")]
        public string DisplayName { get; set; }
        [FGTField("show_desc")]
        public string Description { get; set; }
        [FGTField("round_pool")]
        public List<FGTRoundPool> RoundPool { get; set; }
    }

    public class FGTRoundPool
    {
        [FGTField("only_on_stages")]
        public object OnlyOnStages { get; set; }
        [FGTField("cannot_be_on_stages")]
        public object CannotBeOnStages { get; set; }
        [FGTField("rounds")]
        public List<string> Rounds { get; set; }
    }
}
