using Bonsai;
using Bonsai.Dag;
using Bonsai.Expressions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Test.ClassPropertyMap;

namespace Test.ClassPropertyMap
{
    internal class ClassPropertyMapNameConverter : StringConverter 
    {
        static readonly Attribute[] ExternalizableAttributes = new Attribute[]
        {
            ExternalizableAttribute.Default,
            DesignTimeVisibleAttribute.Yes
        };
        Node<ExpressionBuilder, ExpressionBuilderArgument> GetBuilderNode(ClassPropertyMapBuilder builder, ExpressionBuilderGraph nodeBuilderGraph)
        {
            foreach (var node in nodeBuilderGraph)
            {
                var nodeBuilder = ExpressionBuilder.Unwrap(node.Value);
                if (nodeBuilder == builder)
                {
                    return node;
                }

                var workflowbuilder = nodeBuilder as IWorkflowExpressionBuilder;
                if (workflowbuilder != null && workflowbuilder.Workflow != null)
                {
                    var builderNode = GetBuilderNode(builder, workflowbuilder.Workflow);
                    if (builderNode != null) return builderNode;
                }

            }
            return null;
        }
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            var instance = context.Instance as ClassPropertyMapBuilder;
            var nodeBuilderGraph = (ExpressionBuilderGraph)context.GetService(typeof(ExpressionBuilderGraph));
            if (instance != null && nodeBuilderGraph != null)
            {
                var node = GetBuilderNode(instance, nodeBuilderGraph);
                if  ((node != null) && node.Successors.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        bool HasEditableMembers(Type type)
        {
            bool validType = type.IsClass || (type.IsValueType && !type.IsPrimitive && !type.IsEnum);
            if (!validType) return false;

            var members = type.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            return members.Any(member =>
            (member is PropertyInfo prop && prop.CanWrite && prop.GetSetMethod() != null) ||
            (member is FieldInfo field && !field.IsInitOnly && !field.IsLiteral)
            );
        }

        public override TypeConverter.StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var nodeBuilderGraph = (ExpressionBuilderGraph)context.GetService(typeof(ExpressionBuilderGraph));
            if (nodeBuilderGraph != null)
            {
                var instance = context.Instance as ClassPropertyMapBuilder;
                var builderNode = GetBuilderNode(instance, nodeBuilderGraph);
                if (builderNode != null)
                {
                    var properties = from succesor in builderNode.Successors
                                     let element = ExpressionBuilder.GetWorkflowElement(succesor.Target.Value)
                                     where element != null
                                     select from descriptor in TypeDescriptor.GetProperties(element, ExternalizableAttributes)
                                                                .Cast<PropertyDescriptor>()
                                            where descriptor.IsBrowsable && !descriptor.IsReadOnly && HasEditableMembers(descriptor.PropertyType)
                                            select descriptor;
                    HashSet<PropertyDescriptor> propertySet = null;
                    foreach (var group in properties)
                    {
                        if (propertySet == null)
                        {
                            propertySet = new HashSet<PropertyDescriptor>(group, PropertyDescriptorComparer.Instance);
                        }
                        else propertySet.IntersectWith(group);
                    }
                    return new StandardValuesCollection(propertySet.Select(property => property.Name).ToArray());
                }
            }

            return base.GetStandardValues(context);
        }

        class PropertyDescriptorComparer : IEqualityComparer<PropertyDescriptor>
        {
            public static readonly PropertyDescriptorComparer Instance = new PropertyDescriptorComparer();

            public bool Equals(PropertyDescriptor x, PropertyDescriptor y)
            {
                if (x == null) return y == null;
                else return y != null && x.Name == y.Name && x.PropertyType == y.PropertyType;
            }

            public int GetHashCode(PropertyDescriptor obj)
            {
                var hash = 313;
                hash = hash * 523 + EqualityComparer<string>.Default.GetHashCode(obj.Name);
                hash = hash * 523 + EqualityComparer<Type>.Default.GetHashCode(obj.PropertyType);
                return hash;
            }
        }
    }
}
