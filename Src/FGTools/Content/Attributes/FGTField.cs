using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FGTools.Content.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class FGTField : Attribute
    {
        public string JsonKey { get; }

        public FGTField(string key)
        {
            JsonKey = key;
        }
    }
}
