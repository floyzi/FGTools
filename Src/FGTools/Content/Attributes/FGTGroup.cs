using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FGTools.Content.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class FGTGroup : Attribute
    {
        public string Name { get; }

        public FGTGroup(string name)
        {
            Name = name;
        }
    }
}
