using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FGTools.Content.Attributes
{
    public class FGTContent
    {
        [FGTField("id")]
        public string Id { get; protected set; }
    }
}
