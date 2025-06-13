using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using Bonsai;
using Bonsai.Dag;
using Bonsai.Expressions;


namespace Test.ClassPropertyMap
{

    [DefaultProperty(nameof(PropertyMappings))]
    [WorkflowElementCategory(ElementCategory.Property)]
    [Description("Assigns values of an observable sequence to properties of a class that is a property of a workflow element.")]
    public class NestedPropertyMapBuilder : SingleArgumentExpressionBuilder, INamedElement, IArgumentBuilder
    {

        public new NestedPropertyMapCollection PropertyMappings { get; set; } = new NestedPropertyMapCollection();

        string INamedElement.Name
        {
            get
            {
                if (PropertyMappings.Count > 0)
                {
                    return String.Join(
                        ExpressionHelper.ArgumentSeparator,
                        PropertyMappings.Select(mapping => mapping.Name)
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
            var nodeTree = BuildNodeTree();
            var workflowElement = GetWorkflowElement(successor.Target.Value);
            var instance = Expression.Constant(workflowElement);
            foreach (var node in nodeTree)
            {
                argument = BuildNestedPropertyMapping(argument, instance, node);
            }

            return false;
        }

        internal class NestedPropertyNode
        {
            public List<NestedPropertyNode> Children { get; } = new List<NestedPropertyNode>();
            public string Name { get; set; }
            public string Selector { get; set; }
        }

        List<NestedPropertyNode> BuildNodeTree()
        {
            List<NestedPropertyNode> tree = new List<NestedPropertyNode>();
            foreach (var mapping in PropertyMappings)
            {
                string[] targetPath = mapping.Name.Split(new[] { ExpressionHelper.MemberSeparator }, StringSplitOptions.RemoveEmptyEntries);
                string selector = mapping.Selector;
                BuildNodeBranch(tree, targetPath, selector); 

            }
            return tree;
        }

        internal static void BuildNodeBranch(List<NestedPropertyNode> nodes, string[] targetPath, string selector)
        {
            NestedPropertyNode node = nodes.Find(n => n.Name == targetPath[0]);
            if (node == null)
            {
                NestedPropertyNode newNode = new NestedPropertyNode();
                newNode.Name = targetPath[0];
                nodes.Add(newNode);
                if (targetPath.Length > 1)
                {
                    BuildNodeBranch(newNode.Children, targetPath.Skip(1).ToArray(), selector);
                }
                else //Tip of the branch, this is the member we want to update
                {
                    newNode.Selector = selector;
                }

            }
            else
            {
                if (targetPath.Length > 1)
                {
                    BuildNodeBranch(node.Children, targetPath.Skip(1).ToArray(), selector);
                }
                else
                //This means that the target member of an assignment has been assigned before
                //This should not happen, so we throw an error
                {
                    throw new InvalidOperationException(string.Format("Trying to assign member {0} more than once", string.Join(".", targetPath)));
                }
                
                
            }
        }

        internal static Expression BuildNestedPropertyMapping(Expression source, ConstantExpression instance, NestedPropertyNode root)
        {
            var element = instance.Value;
            if (element is IWorkflowExpressionBuilder workflowBuilder && workflowBuilder.Workflow != null)
            {
                var inputBuilder = (from node in workflowBuilder.Workflow
                                    let externalizedBuilder = Unwrap(node.Value) as IExternalizedMappingBuilder
                                    where externalizedBuilder != null
                                    from workflowProperty in externalizedBuilder.GetExternalizedProperties()
                                    where workflowProperty.ExternalizedName == root.Name //Look for the root property/member
                                    select new { node, workflowProperty }).FirstOrDefault();
                if (inputBuilder == null)
                {
                    throw new InvalidOperationException(string.Format(
                        "The specified property '{0}' was not found in the nested workflow.",
                        root.Name));
                }

                // Checking nested externalized properties requires only one level of indirection
                if (source == EmptyExpression.Instance) return source;

                var argument = source;
                foreach (var successor in inputBuilder.node.Successors)
                {
                    var successorElement = GetWorkflowElement(successor.Target.Value);
                    var successorInstance = Expression.Constant(successorElement);
                    argument = BuildNestedPropertyMapping(argument, successorInstance, root);
                }
                return argument;
            }
            MemberExpression property = default;
            if (element is ICustomTypeDescriptor typeDescriptor)
            {
                var propertyInfo = instance.Type.GetProperty(root.Name);
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

            if (source == EmptyExpression.Instance) return source;

            var sourceType = source.Type.GetGenericArguments()[0];
            var parameter = Expression.Parameter(sourceType);

            var body = BuildNestedAssignmentExpression(instance, parameter, root);

            var actionType = Expression.GetActionType(parameter.Type);
            var action = Expression.Lambda(actionType, body, parameter);
            return Expression.Call(
                typeof(ExpressionBuilder),
                nameof(PropertyMapping),
                new[] { sourceType },
                source,
                action);

        }

        internal static Expression BuildNestedAssignmentExpression(Expression parent, ParameterExpression sourceType, NestedPropertyNode node)
        {
            var member = Expression.PropertyOrField(parent, node.Name);
            List<Expression> body = new List<Expression>();
            if (node.Selector != null)
            {
                var selectorValue = BuildTypeMapping(sourceType, member.Type, node.Selector);
                return Expression.Assign(member, selectorValue);

            }
            else if (member.Type.IsValueType)
            {
                var tmp = Expression.Variable(member.Type, "tmp");
                body.Add(Expression.Assign(tmp, member));
                foreach (var child in node.Children)
                {
                    body.Add(BuildNestedAssignmentExpression(tmp, sourceType, child));
                }
                body.Add(Expression.Assign(member, tmp));
                return Expression.Block(new[] { tmp }, body);

            }
            else
            {
                foreach (var child in node.Children)
                {
                    body.Add(BuildNestedAssignmentExpression(member, sourceType, child));
                }
                return Expression.Block(body);
            }
        
        }
    }

}
