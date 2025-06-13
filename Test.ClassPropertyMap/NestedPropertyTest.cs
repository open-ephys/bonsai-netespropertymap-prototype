using Bonsai;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.ClassPropertyMap
{
    public struct ValueInt
    {
        public int X;
        public int Y;
    }

    public class PropertyInt
    {
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
    }

    public class NestedInt
    {
        public ValueInt valVal = new ValueInt();
        public ValueInt valProp {  get; set; } = new ValueInt();

        public PropertyInt propVal = new PropertyInt();

        public PropertyInt propProp = new PropertyInt();

        public int X;
        public int Y { get; set; } = 0;
    }

    public class NestedPropertyTest : Combinator<Tuple<NestedInt, NestedInt>>
    {
        public NestedInt nestedProp1 {  get; set; } = new NestedInt();
        public NestedInt nestedProp2 { get; set;} = new NestedInt();

        public override IObservable<Tuple<NestedInt,NestedInt>> Process<Tsource>(IObservable<Tsource> source) 
        {
            return source.Select(x => new Tuple<NestedInt, NestedInt>(nestedProp1, nestedProp2));
        }
    
    }
}
