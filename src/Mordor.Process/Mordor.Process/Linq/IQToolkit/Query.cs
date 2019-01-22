// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit
{
    /// <inheritdoc />
    /// <summary>
    ///     A default implementation of IQueryable for use with QueryProvider
    /// </summary>
    public class Query<T> : IOrderedQueryable<T>
    {
        public Query(IQueryProvider provider)
            : this(provider, null)
        {
        }

        public Query(IQueryProvider provider, Type staticType)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Expression = staticType != null ? Expression.Constant(this, staticType) : Expression.Constant(this);
        }

        public Query(QueryProvider provider, Expression expression)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            if (!typeof(IQueryable<T>).IsAssignableFrom(expression.Type))
                throw new ArgumentOutOfRangeException(nameof(expression));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Expression = expression;
        }

        public string QueryText => Provider is IQueryText iqt ? iqt.GetQueryText(Expression) : "";

        public Expression Expression { get; }

        public Type ElementType => typeof(T);

        public IQueryProvider Provider { get; }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>) Provider.Execute(Expression)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) Provider.Execute(Expression)).GetEnumerator();
        }

        public override string ToString()
        {
            if (Expression.NodeType == ExpressionType.Constant && ((ConstantExpression) Expression).Value == this)
                return "Query(" + typeof(T) + ")";

            return Expression.ToString();
        }
    }
}