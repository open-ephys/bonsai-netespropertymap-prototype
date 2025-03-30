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
    /// <summary>
    /// Represents a dynamic assignment between a selected input source and a property of
    /// a class that is a property of a workflow element.
    /// </summary>
    public sealed class ClassPropertyMapping
    {
        /// <summary>
        /// Initialized a new instance of the <see cref="ClassPropertyMapping"/> class.
        /// </summary>
        /// 
        public ClassPropertyMapping() { }

        public ClassPropertyMapping(string name, string selector)
        {
            Name = name;
            Selector = selector;
        }

        [TypeConverter(typeof(ClassPropertyMappingNameConverter))]
        public string Name { get; set; }
        
        [XmlAttribute]
        [DefaultValue("")]
        [Editor("Bonsai.Design.MultiMemberSelectorEditor, Bonsai.Design", DesignTypes.UITypeEditor)]
        public string Selector { get; set; }

    }
}
