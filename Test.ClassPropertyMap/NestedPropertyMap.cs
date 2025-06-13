using Bonsai;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Test.ClassPropertyMap
{
    public sealed class NestedPropertyMap
    {
        /// <summary>
        /// Initialized a new instance of the <see cref="NestedPropertyMap"/> class.
        /// </summary>
        /// 
        public NestedPropertyMap() { }

        public NestedPropertyMap(string name, string selector)
        {
            Name = name;
            Selector = selector;
        }

        public string Name { get; set; }

        [DefaultValue("")]
        public string Selector { get; set; }
    }
}
