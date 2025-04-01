using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
using System.Xml.Serialization;
using Bonsai;
using Bonsai.Dag;
using Bonsai.Expressions;
using Test.ClassPropertyMap;

namespace Test.ClassPropertyMap
{
    [DefaultProperty(nameof(PropertyMappings))]
    [WorkflowElementCategory(ElementCategory.Property)]
    [Description("Assigns values of an observable sequence to properties of a class that is a property of a workflow element.")]
    public class ClassPropertyMapBuilder : SingleArgumentExpressionBuilder, INamedElement, IArgumentBuilder
    {
        public new ClassPropertyMapCollection PropertyMappings { get; set; } = new ClassPropertyMapCollection();

        [TypeConverter(typeof(ClassPropertyMapNameConverter))]
        public string MemberName { get; set; }

        string INamedElement.Name {
            get
            { 
                if (!String.IsNullOrEmpty(MemberName) && PropertyMappings.Count > 0)
                {
                    return String.Join(
                        ExpressionHelper.ArgumentSeparator,
                        PropertyMappings.Select(mapping => MemberName + ExpressionHelper.MemberSeparator + mapping.Name)
                        );
                }
                return GetElementDisplayName(GetType());
            }
        }

        public override Expression Build(IEnumerable<Expression> arguments)
        {
            return arguments.First();
        }

        bool IArgumentBuilder.BuildArgument(Expression source, Edge<ExpressionBuilder, ExpressionBuilderArgument> successor, out Expression argument)
        {
            argument = source;
            var workflowElement = GetWorkflowElement(successor.Target.Value);
            var instance = Expression.Constant(workflowElement);
            foreach (var mapping in PropertyMappings)
            {
                argument = BuildClassPropertyMapping(argument, instance, MemberName, mapping.Name, mapping.Selector);
            }

            return false;
        }

        internal static Expression BuildClassPropertyMapping(Expression source, ConstantExpression instance, string baseMemberName, string targetMemberName, string sourceSelector)
        {
            var element = instance.Value;
            if (element is IWorkflowExpressionBuilder workflowBuilder && workflowBuilder.Workflow != null)
            {
                var inputBuilder = (from node in workflowBuilder.Workflow
                                    let externalizedBuilder = Unwrap(node.Value) as IExternalizedMappingBuilder
                                    where externalizedBuilder != null
                                    from workflowProperty in externalizedBuilder.GetExternalizedProperties()
                                    where workflowProperty.ExternalizedName == baseMemberName
                                    select new { node, workflowProperty }).FirstOrDefault();
                if (inputBuilder == null)
                {
                    throw new InvalidOperationException(string.Format(
                        "The specified property '{0}' was not found in the nested workflow.",
                        baseMemberName));
                }

                // Checking nested externalized properties requires only one level of indirection
                if (source == EmptyExpression.Instance) return source;

                var argument = source;
                foreach (var successor in inputBuilder.node.Successors)
                {
                    var successorElement = GetWorkflowElement(successor.Target.Value);
                    var successorInstance = Expression.Constant(successorElement);
                    argument = BuildPropertyMapping(argument, successorInstance, inputBuilder.workflowProperty.Name, sourceSelector);
                }
                return argument;
            }

            MemberExpression property = default;
            if (element is ICustomTypeDescriptor typeDescriptor)
            {
                var propertyInfo = instance.Type.GetProperty(baseMemberName);
                if (propertyInfo == null)
                {
                    var propertyOwner = typeDescriptor.GetPropertyOwner(null);
                    if (propertyOwner != instance.Value)
                    {
                        instance = Expression.Constant(propertyOwner);
                    }
                }
                else property = Expression.Property(instance, propertyInfo);
            }

            property ??= Expression.Property(instance, baseMemberName);
            if (source == EmptyExpression.Instance) return source;

        
            var sourceType = source.Type.GetGenericArguments()[0];
            var parameter = Expression.Parameter(sourceType);
            string[] targetPath = targetMemberName.Split(new[] { ExpressionHelper.MemberSeparator }, StringSplitOptions.RemoveEmptyEntries);

            var body = BuildMemberAssignment(property, targetPath, parameter, sourceSelector);
            

            var actionType = Expression.GetActionType(parameter.Type);
            var action = Expression.Lambda(actionType, body, parameter);
            return Expression.Call(
                typeof(ExpressionBuilder),
                nameof(PropertyMapping),
                new[] { sourceType },
                source,
                action);
        }

        internal static Expression BuildMemberAssignment(MemberExpression baseMember, string[] childMemberPath,  ParameterExpression sourceType, string sourceSelector)
        {
            Expression body;
            if (baseMember.Type.IsValueType)
            {
                var temp = Expression.Variable(baseMember.Type);
                var origVal = Expression.Assign(temp, baseMember);

                var childMemberName = childMemberPath[0];
                var child = Expression.PropertyOrField(temp, childMemberName);

                Expression assignVal;
                Expression newVal;
                if (childMemberPath.Length > 1)
                {
                    assignVal = BuildMemberAssignment(child, childMemberPath.Skip(1).ToArray(), sourceType, sourceSelector);
                }
                else
                {
                    newVal = BuildTypeMapping(sourceType, child.Type, sourceSelector);
                    assignVal = Expression.Assign(child, newVal);

                }
                

                var writeBack = Expression.Assign(baseMember, temp);
                body = Expression.Block(new[] { temp }, origVal, assignVal, writeBack);

            }
            else
            {
                var childMemberName = childMemberPath[0];
                var child = Expression.PropertyOrField(baseMember, childMemberName);
                Expression newVal;
                if (childMemberPath.Length > 1)
                {
                    newVal = BuildMemberAssignment(child, childMemberPath.Skip(1).ToArray(), sourceType, sourceSelector);
                }
                else
                {
                    newVal = BuildTypeMapping(sourceType, child.Type, sourceSelector);
                }
                body = Expression.Assign(child, newVal);
            }
            return body;
        }

        
    }  

}
