// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    /// <summary>
    /// Removes column declarations in SelectExpression's that are not referenced
    /// </summary>
    public class UnusedColumnRemover : DbExpressionVisitor
    {
        private readonly Dictionary<TableAlias, HashSet<string>> _allColumnsUsed;
        private bool _retainAllColumns;

        private UnusedColumnRemover()
        {
            _allColumnsUsed = new Dictionary<TableAlias, HashSet<string>>();
        }

        public static Expression Remove(Expression expression) 
        {
            return new UnusedColumnRemover().Visit(expression);
        }

        private void MarkColumnAsUsed(TableAlias alias, string name)
        {
            HashSet<string> columns;
            if (!_allColumnsUsed.TryGetValue(alias, out columns))
            {
                columns = new HashSet<string>();
                _allColumnsUsed.Add(alias, columns);
            }
            columns.Add(name);
        }

        private bool IsColumnUsed(TableAlias alias, string name)
        {
            HashSet<string> columnsUsed;
            if (_allColumnsUsed.TryGetValue(alias, out columnsUsed))
            {
                if (columnsUsed != null)
                {
                    return columnsUsed.Contains(name);
                }
            }
            return false;
        }

        private void ClearColumnsUsed(TableAlias alias)
        {
            _allColumnsUsed[alias] = new HashSet<string>();
        }

        protected override Expression VisitColumn(ColumnExpression column)
        {
            MarkColumnAsUsed(column.Alias, column.Name);
            return column;
        }

        protected override Expression VisitSubquery(SubqueryExpression subquery) 
        {
            if ((subquery.NodeType == (ExpressionType)DbExpressionType.Scalar ||
                subquery.NodeType == (ExpressionType)DbExpressionType.In) &&
                subquery.Select != null) 
            {
                Debug.Assert(subquery.Select.Columns.Count == 1);
                MarkColumnAsUsed(subquery.Select.Alias, subquery.Select.Columns[0].Name);
            }
 	        return base.VisitSubquery(subquery);
        }

        protected override Expression VisitSelect(SelectExpression select)
        {
            // visit column projection first
            var columns = select.Columns;

            var wasRetained = _retainAllColumns;
            _retainAllColumns = false;

            List<ColumnDeclaration> alternate = null;
            for (int i = 0, n = select.Columns.Count; i < n; i++)
            {
                var decl = select.Columns[i];
                if (wasRetained || select.IsDistinct || IsColumnUsed(select.Alias, decl.Name))
                {
                    var expr = Visit(decl.Expression);
                    if (expr != decl.Expression)
                    {
                        decl = new ColumnDeclaration(decl.Name, expr, decl.QueryType);
                    }
                }
                else
                {
                    decl = null;  // null means it gets omitted
                }
                if (decl != select.Columns[i] && alternate == null)
                {
                    alternate = new List<ColumnDeclaration>();
                    for (var j = 0; j < i; j++)
                    {
                        alternate.Add(select.Columns[j]);
                    }
                }
                if (decl != null && alternate != null)
                {
                    alternate.Add(decl);
                }
            }
            if (alternate != null)
            {
                columns = alternate.AsReadOnly();
            }

            var take = Visit(select.Take);
            var skip = Visit(select.Skip);
            var groupbys = VisitExpressionList(select.GroupBy);
            var orderbys = VisitOrderBy(select.OrderBy);
            var where = Visit(select.Where);

            var from = Visit(select.From);

            ClearColumnsUsed(select.Alias);

            if (columns != select.Columns 
                || take != select.Take 
                || skip != select.Skip
                || orderbys != select.OrderBy 
                || groupbys != select.GroupBy
                || where != select.Where 
                || from != select.From)
            {
                select = new SelectExpression(select.Alias, columns, from, where, orderbys, groupbys, select.IsDistinct, skip, take, select.IsReverse);
            }

            _retainAllColumns = wasRetained;

            return select;
        }

        protected override Expression VisitAggregate(AggregateExpression aggregate)
        {
            // COUNT(*) forces all columns to be retained in subquery
            if (aggregate.AggregateName == "Count" && aggregate.Argument == null)
            {
                _retainAllColumns = true;
            }
            return base.VisitAggregate(aggregate);
        }

        protected override Expression VisitProjection(ProjectionExpression projection)
        {
            // visit mapping in reverse order
            var projector = Visit(projection.Projector);
            var select = (SelectExpression)Visit(projection.Select);
            return UpdateProjection(projection, select, projector, projection.Aggregator);
        }

        protected override Expression VisitClientJoin(ClientJoinExpression join)
        {
            var innerKey = VisitExpressionList(join.InnerKey);
            var outerKey = VisitExpressionList(join.OuterKey);
            var projection = (ProjectionExpression)Visit(join.Projection);
            if (projection != join.Projection || innerKey != join.InnerKey || outerKey != join.OuterKey)
            {
                return new ClientJoinExpression(projection, outerKey, innerKey);
            }
            return join;
        }

        protected override Expression VisitJoin(JoinExpression join)
        {
            if (join.Join == JoinType.SingletonLeftOuter)
            {
                // first visit right side w/o looking at condition
                var right = Visit(join.Right);
                var ax = right as AliasedExpression;
                if (ax != null && !_allColumnsUsed.ContainsKey(ax.Alias))
                {
                    // if nothing references the alias on the right, then the join is redundant
                    return Visit(join.Left);
                }
                // otherwise do it the right way
                var cond = Visit(join.Condition);
                var left = Visit(join.Left);
                right = Visit(join.Right);
                return UpdateJoin(join, join.Join, left, right, cond);
            }
            else
            {
                // visit join in reverse order
                var condition = Visit(join.Condition);
                var right = VisitSource(join.Right);
                var left = VisitSource(join.Left);
                return UpdateJoin(join, join.Join, left, right, condition);
            }
        }
    }
}