using System;

namespace FGTools.Content.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class FGTField(string key) : Attribute
    {
        public string JsonKey { get; } = key;
    }
}
