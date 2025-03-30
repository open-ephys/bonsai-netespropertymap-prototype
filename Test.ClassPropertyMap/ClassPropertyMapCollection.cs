using Bonsai.Expressions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bonsai.Expressions;

namespace Test.ClassPropertyMap
{
    public class ClassPropertyMapCollection : KeyedCollection<string, ClassPropertyMapping>
    {

        protected override string GetKeyForItem(ClassPropertyMapping item)
        {
            return item.Name;
        }
    }
}
