using Bonsai.Dag;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using Bonsai.Expressions;
using Test.ClassPropertyMap;
using Bonsai;
using System.Reflection;

namespace Test.ClassPropertyMap
{
    // This is heavily copied from Bonsai.Core.Expressions.MappingNameConverter
    // Ideally I would have used inheritance, but access does not allow for that
    internal class ClassPropertyMappingNameConverter : StringConverter
    {
        static readonly Attribute[] ExternalizableAttributes = new Attribute[]
        {
            ExternalizableAttribute.Default,
            DesignTimeVisibleAttribute.Yes
        };
        protected bool ContainsMapping(ExpressionBuilder builder, ClassPropertyMapping mapping)
        {
            return builder is ClassPropertyMapBuilder mappingBuilder && mappingBuilder.PropertyMappings.Contains(mapping);
        }

        Node<ExpressionBuilder, ExpressionBuilderArgument> GetBuilderNode(ClassPropertyMapping mapping, ExpressionBuilderGraph nodeBuilderGraph, out ExpressionBuilder builderObject)
        {
            foreach (var node in nodeBuilderGraph)
            {
                var builder = ExpressionBuilder.Unwrap(node.Value);
                if (ContainsMapping(builder, mapping))
                {
                    builderObject = builder;
                    return node;
                }

                var workflowBuilder = builder as IWorkflowExpressionBuilder;
                if (workflowBuilder != null && workflowBuilder.Workflow != null)
                {
                    ExpressionBuilder tmpBuilderObject;
                    var builderNode = GetBuilderNode(mapping, workflowBuilder.Workflow, out tmpBuilderObject);
                    if (builderNode != null)
                    {
                        builderObject = tmpBuilderObject;
                        return builderNode;
                    }
                }
            }
            builderObject = null;
            return null;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            var nodeBuilderGraph = (ExpressionBuilderGraph)context.GetService(typeof(ExpressionBuilderGraph));
            if (nodeBuilderGraph != null)
            {
                var mapping = (ClassPropertyMapping)context.Instance;
                if (mapping != null)
                {
                    ExpressionBuilder builderObject;
                    var builderNode = GetBuilderNode(mapping, nodeBuilderGraph, out builderObject);
                    if (builderNode != null && builderNode.Successors.Count > 0 && builderObject is ClassPropertyMapBuilder mappingBuilder && !String.IsNullOrEmpty(mappingBuilder.MemberName))
                    {
                        var hasProp = builderNode.Successors.Any(succesor => {
                            var element = ExpressionBuilder.GetWorkflowElement(succesor.Target.Value);
                            return (TypeDescriptor.GetProperties(element, ExternalizableAttributes).Find(mappingBuilder.MemberName, false) != null);
                        });
                        return hasProp;

                    }
                }
            }
            return false;
        }

        public override TypeConverter.StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var nodeBuilderGraph = (ExpressionBuilderGraph)context.GetService(typeof(ExpressionBuilderGraph));
            if (nodeBuilderGraph != null)
            {
                var mapping = (ClassPropertyMapping)context.Instance;
                ExpressionBuilder builderObject;
                var builderNode = GetBuilderNode(mapping, nodeBuilderGraph, out builderObject);
                if (builderNode != null)
                {
                    var members = from successor in builderNode.Successors
                                  let element = ExpressionBuilder.GetWorkflowElement(successor.Target.Value)
                                  where element != null
                                  let property = TypeDescriptor.GetProperties(element, ExternalizableAttributes).Find((builderObject as ClassPropertyMapBuilder).MemberName, false)
                                  where property != null
                                  let type = property.PropertyType
                                  let properties = TypeDescriptor.GetProperties(type, ExternalizableAttributes).Cast<PropertyDescriptor>()
                                                                .Where(d => d.IsBrowsable && !d.IsReadOnly).Select(p => type.GetProperty(p.Name))
                                  let fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                                  select from member in properties.Cast<MemberInfo>().Concat(fields.Cast<MemberInfo>())
                                         select member;

                    HashSet<MemberInfo> memberSet = null;
                    foreach (var group in members)
                    {
                        if (memberSet == null)
                        {
                            memberSet = new HashSet<MemberInfo>(group, MemberInfoComparer.Instance);
                        }
                        else memberSet.IntersectWith(group);
                    }
                    return new StandardValuesCollection(memberSet.Select(property => property.Name).ToArray());
                }
            }
            return base.GetStandardValues(context);
        }
    }

    class MemberInfoComparer : IEqualityComparer<MemberInfo>
    {
        public static readonly MemberInfoComparer Instance = new MemberInfoComparer();

        public bool Equals(MemberInfo x, MemberInfo y)
        {
            if (x == null) return y == null;
            else return y != null && x.Name == y.Name && GetMemberType(x) == GetMemberType(y);
        }

        public int GetHashCode(MemberInfo obj)
        {
            var hash = 313;
            hash = hash * 523 + EqualityComparer<string>.Default.GetHashCode(obj.Name);
            hash = hash * 523 + EqualityComparer<Type>.Default.GetHashCode(GetMemberType(obj));
            return hash;
        }

        Type GetMemberType(MemberInfo member)
        {
            return member.MemberType switch
            {
                MemberTypes.Property => ((PropertyInfo)member).PropertyType,
                MemberTypes.Field => ((FieldInfo)member).FieldType,
                _ => throw new ArgumentException("Unexpected member type ", nameof(member))

            };
        }

    }
}
