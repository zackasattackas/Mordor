// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    public class ComparisonRewriter : DbExpressionVisitor
    {
        private readonly QueryMapping _mapping;

        private ComparisonRewriter(QueryMapping mapping)
        {
            _mapping = mapping;
        }

        public static Expression Rewrite(QueryMapping mapping, Expression expression)
        {
            return new ComparisonRewriter(mapping).Visit(expression);
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            switch (b.NodeType)
            {
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                    var result = Compare(b);
                    if (result == b)
                        goto default;
                    return Visit(result);
                default:
                    return base.VisitBinary(b);
            }
        }

        protected Expression SkipConvert(Expression expression)
        {
            while (expression.NodeType == ExpressionType.Convert)
            {
                expression = ((UnaryExpression)expression).Operand;
            }
            return expression;
        }

        protected Expression Compare(BinaryExpression bop)
        {
            var e1 = SkipConvert(bop.Left);
            var e2 = SkipConvert(bop.Right);

            var oj1 = e1 as OuterJoinedExpression;
            var oj2 = e2 as OuterJoinedExpression;

            var entity1 = oj1 != null ? oj1.Expression as EntityExpression : e1 as EntityExpression;
            var entity2 = oj2 != null ? oj2.Expression as EntityExpression : e2 as EntityExpression;

            var negate = bop.NodeType == ExpressionType.NotEqual;

            // check for outer-joined entity comparing against null. These are special because outer joins have 
            // a test expression specifically desgined to be tested against null to determine if the joined side exists.
            if (oj1 != null && e2.NodeType == ExpressionType.Constant && ((ConstantExpression)e2).Value == null)
            {
                return MakeIsNull(oj1.Test, negate);
            }

            if (oj2 != null && e1.NodeType == ExpressionType.Constant && ((ConstantExpression)e1).Value == null)
            {
                return MakeIsNull(oj2.Test, negate);
            }

            // if either side is an entity construction expression then compare using its primary key members
            if (entity1 != null)
            {
                return MakePredicate(e1, e2, _mapping.GetPrimaryKeyMembers(entity1.Entity), negate);
            }

            if (entity2 != null)
            {
                return MakePredicate(e1, e2, _mapping.GetPrimaryKeyMembers(entity2.Entity), negate);
            }

            // check for comparison of user constructed type projections
            var dm1 = GetDefinedMembers(e1);
            var dm2 = GetDefinedMembers(e2);

            if (dm1 == null && dm2 == null)
            {
                // neither are constructed types
                return bop;
            }

            if (dm1 != null && dm2 != null)
            {
                // both are constructed types, so they'd better have the same members declared
                var names1 = new HashSet<string>(dm1.Select(m => m.Name));
                var names2 = new HashSet<string>(dm2.Select(m => m.Name));
                if (names1.IsSubsetOf(names2) && names2.IsSubsetOf(names1)) 
                {
                    return MakePredicate(e1, e2, dm1, negate);
                }
            }
            else if (dm1 != null)
            {
                return MakePredicate(e1, e2, dm1, negate);
            }
            else if (dm2 != null)
            {
                return MakePredicate(e1, e2, dm2, negate);
            }

            throw new InvalidOperationException("Cannot compare two constructed types with different sets of members assigned.");
        }

        protected Expression MakeIsNull(Expression expression, bool negate)
        {
            Expression isnull = new IsNullExpression(expression);
            return negate ? Expression.Not(isnull) : isnull;
        }

        protected Expression MakePredicate(Expression e1, Expression e2, IEnumerable<MemberInfo> members, bool negate)
        {
            var pred = members.Select(m =>
                QueryBinder.BindMember(e1, m).Equal(QueryBinder.BindMember(e2, m))
                ).Join(ExpressionType.And);
            if (negate)
                pred = Expression.Not(pred);
            return pred;
        }

        private IEnumerable<MemberInfo> GetDefinedMembers(Expression expr)
        {
            var mini = expr as MemberInitExpression;
            if (mini != null)
            {
                var members = mini.Bindings.Select(b => FixMember(b.Member));
                if (mini.NewExpression.Members != null)
                {
                    members.Concat(mini.NewExpression.Members.Select(m => FixMember(m)));
                }
                return members;
            }

            var nex = expr as NewExpression;
            if (nex != null && nex.Members != null)
            {
                return nex.Members.Select(m => FixMember(m));
            }
            return null;
        }

        private static MemberInfo FixMember(MemberInfo member)
        {
            if (member.MemberType == MemberTypes.Method && member.Name.StartsWith("get_"))
            {
                return member.DeclaringType.GetProperty(member.Name.Substring(4));
            }
            return member;
        }
    }
}
