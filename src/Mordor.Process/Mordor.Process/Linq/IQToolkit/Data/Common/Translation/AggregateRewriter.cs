// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    /// <summary>
    /// Rewrite aggregate expressions, moving them into same select expression that has the group-by clause
    /// </summary>
    public class AggregateRewriter : DbExpressionVisitor
    {
        private readonly QueryLanguage _language;
        private readonly ILookup<TableAlias, AggregateSubqueryExpression> _lookup;
        private readonly Dictionary<AggregateSubqueryExpression, Expression> _map;

        private AggregateRewriter(QueryLanguage language, Expression expr)
        {
            _language = language;
            _map = new Dictionary<AggregateSubqueryExpression, Expression>();
            _lookup = AggregateGatherer.Gather(expr).ToLookup(a => a.GroupByAlias);
        }

        public static Expression Rewrite(QueryLanguage language, Expression expr)
        {
            return new AggregateRewriter(language, expr).Visit(expr);
        }

        protected override Expression VisitSelect(SelectExpression select)
        {
            select = (SelectExpression)base.VisitSelect(select);
            if (_lookup.Contains(select.Alias))
            {
                var aggColumns = new List<ColumnDeclaration>(select.Columns);
                foreach (var ae in _lookup[select.Alias])
                {
                    var name = "agg" + aggColumns.Count;
                    var colType = _language.TypeSystem.GetColumnType(ae.Type);
                    var cd = new ColumnDeclaration(name, ae.AggregateInGroupSelect, colType);
                    _map.Add(ae, new ColumnExpression(ae.Type, colType, ae.GroupByAlias, name));
                    aggColumns.Add(cd);
                }
                return new SelectExpression(select.Alias, aggColumns, select.From, select.Where, select.OrderBy, select.GroupBy, select.IsDistinct, select.Skip, select.Take, select.IsReverse);
            }
            return select;
        }

        protected override Expression VisitAggregateSubquery(AggregateSubqueryExpression aggregate)
        {
            Expression mapped;
            if (_map.TryGetValue(aggregate, out mapped))
            {
                return mapped;
            }
            return Visit(aggregate.AggregateAsSubquery);
        }

        private class AggregateGatherer : DbExpressionVisitor
        {
            private readonly List<AggregateSubqueryExpression> _aggregates = new List<AggregateSubqueryExpression>();
            private AggregateGatherer()
            {
            }

            internal static List<AggregateSubqueryExpression> Gather(Expression expression)
            {
                var gatherer = new AggregateGatherer();
                gatherer.Visit(expression);
                return gatherer._aggregates;
            }

            protected override Expression VisitAggregateSubquery(AggregateSubqueryExpression aggregate)
            {
                _aggregates.Add(aggregate);
                return base.VisitAggregateSubquery(aggregate);
            }
        }
    }
}