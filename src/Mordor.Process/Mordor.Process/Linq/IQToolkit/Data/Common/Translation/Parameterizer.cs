// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    /// <summary>
    /// Converts user arguments into named-value parameters
    /// </summary>
    public class Parameterizer : DbExpressionVisitor
    {
        private readonly QueryLanguage _language;
        private readonly Dictionary<TypeAndValue, NamedValueExpression> _map = new Dictionary<TypeAndValue, NamedValueExpression>();
        private readonly Dictionary<HashedExpression, NamedValueExpression> _pmap = new Dictionary<HashedExpression, NamedValueExpression>();

        private Parameterizer(QueryLanguage language)
        {
            _language = language;
        }

        public static Expression Parameterize(QueryLanguage language, Expression expression)
        {
            return new Parameterizer(language).Visit(expression);
        }

        protected override Expression VisitProjection(ProjectionExpression proj)
        {
            // don't parameterize the projector or aggregator!
            var select = (SelectExpression)Visit(proj.Select);
            return UpdateProjection(proj, select, proj.Projector, proj.Aggregator);
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            if (u.NodeType == ExpressionType.Convert && u.Operand.NodeType == ExpressionType.ArrayIndex)
            {
                var b = (BinaryExpression)u.Operand;
                if (IsConstantOrParameter(b.Left) && IsConstantOrParameter(b.Right))
                {
                    return GetNamedValue(u);
                }
            }
            return base.VisitUnary(u);
        }

        private static bool IsConstantOrParameter(Expression e)
        {
            return e != null && e.NodeType == ExpressionType.Constant || e.NodeType == ExpressionType.Parameter;
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            var left = Visit(b.Left);
            var right = Visit(b.Right);
            if (left.NodeType == (ExpressionType)DbExpressionType.NamedValue
             && right.NodeType == (ExpressionType)DbExpressionType.Column)
            {
                var nv = (NamedValueExpression)left;
                var c = (ColumnExpression)right;
                left = new NamedValueExpression(nv.Name, c.QueryType, nv.Value);
            }
            else if (right.NodeType == (ExpressionType)DbExpressionType.NamedValue
             && left.NodeType == (ExpressionType)DbExpressionType.Column)
            {
                var nv = (NamedValueExpression)right;
                var c = (ColumnExpression)left;
                right = new NamedValueExpression(nv.Name, c.QueryType, nv.Value);
            }
            return UpdateBinary(b, left, right, b.Conversion, b.IsLiftedToNull, b.Method);
        }

        protected override ColumnAssignment VisitColumnAssignment(ColumnAssignment ca)
        {
            ca = base.VisitColumnAssignment(ca);
            var expression = ca.Expression;
            var nv = expression as NamedValueExpression;
            if (nv != null)
            {
                expression = new NamedValueExpression(nv.Name, ca.Column.QueryType, nv.Value);
            }
            return UpdateColumnAssignment(ca, ca.Column, expression);
        }

        private int _iParam;
        protected override Expression VisitConstant(ConstantExpression c)
        {
            if (c.Value != null && !IsNumeric(c.Value.GetType())) {
                NamedValueExpression nv;
                var tv = new TypeAndValue(c.Type, c.Value);
                if (!_map.TryGetValue(tv, out nv)) { // re-use same name-value if same type & value
                    var name = "p" + (_iParam++);
                    nv = new NamedValueExpression(name, _language.TypeSystem.GetColumnType(c.Type), c);
                    _map.Add(tv, nv);
                }
                return nv;
            }
            return c;
        }

        protected override Expression VisitParameter(ParameterExpression p) 
        {
            return GetNamedValue(p);
        }

        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            m = (MemberExpression)base.VisitMemberAccess(m);
            var nv = m.Expression as NamedValueExpression;
            if (nv != null) 
            {
                Expression x = Expression.MakeMemberAccess(nv.Value, m.Member);
                return GetNamedValue(x);
            }
            return m;
        }

        private Expression GetNamedValue(Expression e)
        {
            NamedValueExpression nv;
            var he = new HashedExpression(e);
            if (!_pmap.TryGetValue(he, out nv))
            {
                var name = "p" + (_iParam++);
                nv = new NamedValueExpression(name, _language.TypeSystem.GetColumnType(e.Type), e);
                _pmap.Add(he, nv);
            }
            return nv;
        }

        private bool IsNumeric(Type type)
        {
            switch (Type.GetTypeCode(type)) {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        private struct TypeAndValue : IEquatable<TypeAndValue>
        {
            private readonly Type _type;
            private readonly object _value;
            private readonly int _hash;

            public TypeAndValue(Type type, object value)
            {
                _type = type;
                _value = value;
                _hash = type.GetHashCode() + (value != null ? value.GetHashCode() : 0);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is TypeAndValue))
                    return false;
                return Equals((TypeAndValue)obj);
            }

            public bool Equals(TypeAndValue vt)
            {
                return vt._type == _type && Equals(vt._value, _value);
            }

            public override int GetHashCode()
            {
                return _hash;
            }
        }

        private struct HashedExpression : IEquatable<HashedExpression>
        {
            private readonly Expression _expression;
            private readonly int _hashCode;

            public HashedExpression(Expression expression)
            {
                _expression = expression;
                _hashCode = Hasher.ComputeHash(expression);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is HashedExpression))
                    return false;
 	            return Equals((HashedExpression)obj);
            }

            public bool Equals(HashedExpression other)
            {
                return _hashCode == other._hashCode &&
                    DbExpressionComparer.AreEqual(_expression, other._expression);
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }

            private class Hasher : DbExpressionVisitor
            {
                private int _hc;

                internal static int ComputeHash(Expression expression)
                {
                    var hasher = new Hasher();
                    hasher.Visit(expression);
                    return hasher._hc;
                }

                protected override Expression VisitConstant(ConstantExpression c)
                {
                    _hc = _hc + ((c.Value != null) ? c.Value.GetHashCode() : 0);
                    return c;
                }
            }
        }
    }
}
