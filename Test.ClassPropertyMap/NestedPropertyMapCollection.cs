using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.ClassPropertyMap
{
    public class NestedPropertyMapCollection : KeyedCollection<string, NestedPropertyMap>
    {
        protected override string GetKeyForItem(NestedPropertyMap item)
        {
            return item.Name;
        }
    }
}
