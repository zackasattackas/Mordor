// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    /// <summary>
    /// Moves order-bys to the outermost select if possible
    /// </summary>
    public class OrderByRewriter : DbExpressionVisitor
    {
        private readonly QueryLanguage _language;
        private IList<OrderExpression> _gatheredOrderings;
        private bool _isOuterMostSelect;

        private OrderByRewriter(QueryLanguage language)
        {
            _language = language;
            _isOuterMostSelect = true;
        }

        public static Expression Rewrite(QueryLanguage language, Expression expression)
        {
            return new OrderByRewriter(language).Visit(expression);
        }

        protected override Expression VisitSelect(SelectExpression select)
        {
            var saveIsOuterMostSelect = _isOuterMostSelect;
            try
            {
                _isOuterMostSelect = false;
                select = (SelectExpression)base.VisitSelect(select);

                var hasOrderBy = select.OrderBy != null && select.OrderBy.Count > 0;
                var hasGroupBy = select.GroupBy != null && select.GroupBy.Count > 0;
                var canHaveOrderBy = saveIsOuterMostSelect || select.Take != null || select.Skip != null;
                var canReceiveOrderings = canHaveOrderBy && !hasGroupBy && !select.IsDistinct && !AggregateChecker.HasAggregates(select);

                if (hasOrderBy)
                {
                    PrependOrderings(select.OrderBy);
                }

                if (select.IsReverse)
                {
                    ReverseOrderings();
                }

                IEnumerable<OrderExpression> orderings = null;
                if (canReceiveOrderings)
                {
                    orderings = _gatheredOrderings;
                }
                else if (canHaveOrderBy)
                {
                    orderings = select.OrderBy;
                }
                var canPassOnOrderings = !saveIsOuterMostSelect && !hasGroupBy && !select.IsDistinct;
                var columns = select.Columns;
                if (_gatheredOrderings != null)
                {
                    if (canPassOnOrderings)
                    {
                        var producedAliases = DeclaredAliasGatherer.Gather(select.From);
                        // reproject order expressions using this select's alias so the outer select will have properly formed expressions
                        var project = RebindOrderings(_gatheredOrderings, select.Alias, producedAliases, select.Columns);
                        _gatheredOrderings = null;
                        PrependOrderings(project.Orderings);
                        columns = project.Columns;
                    }
                    else
                    {
                        _gatheredOrderings = null;
                    }
                }
                if (orderings != select.OrderBy || columns != select.Columns || select.IsReverse)
                {
                    select = new SelectExpression(select.Alias, columns, select.From, select.Where, orderings, select.GroupBy, select.IsDistinct, select.Skip, select.Take, false);
                }
                return select;
            }
            finally
            {
                _isOuterMostSelect = saveIsOuterMostSelect;
            }
        }

        protected override Expression VisitSubquery(SubqueryExpression subquery)
        {
            var saveOrderings = _gatheredOrderings;
            _gatheredOrderings = null;
            var result = base.VisitSubquery(subquery);
            _gatheredOrderings = saveOrderings;
            return result;
        }

        protected override Expression VisitJoin(JoinExpression join)
        {
            // make sure order by expressions lifted up from the left side are not lost
            // when visiting the right side
            var left = VisitSource(join.Left);
            var leftOrders = _gatheredOrderings;
            _gatheredOrderings = null; // start on the right with a clean slate
            var right = VisitSource(join.Right);
            PrependOrderings(leftOrders);
            var condition = Visit(join.Condition);
            if (left != join.Left || right != join.Right || condition != join.Condition)
            {
                return new JoinExpression(join.Join, left, right, condition);
            }
            return join;
        }

        /// <summary>
        /// Add a sequence of order expressions to an accumulated list, prepending so as
        /// to give precedence to the new expressions over any previous expressions
        /// </summary>
        /// <param name="newOrderings"></param>
        protected void PrependOrderings(IList<OrderExpression> newOrderings)
        {
            if (newOrderings != null)
            {
                if (_gatheredOrderings == null)
                {
                    _gatheredOrderings = new List<OrderExpression>();
                }
                for (var i = newOrderings.Count - 1; i >= 0; i--)
                {
                    _gatheredOrderings.Insert(0, newOrderings[i]);
                }
                // trim off obvious duplicates
                var unique = new HashSet<string>();
                for (var i = 0; i < _gatheredOrderings.Count;) 
                {
                    var column = _gatheredOrderings[i].Expression as ColumnExpression;
                    if (column != null)
                    {
                        var hash = column.Alias + ":" + column.Name;
                        if (unique.Contains(hash))
                        {
                            _gatheredOrderings.RemoveAt(i);
                            // don't increment 'i', just continue
                            continue;
                        }

                        unique.Add(hash);
                    }
                    i++;
                }
            }
        }

        protected void ReverseOrderings()
        {
            if (_gatheredOrderings != null)
            {
                for (int i = 0, n = _gatheredOrderings.Count; i < n; i++)
                {
                    var ord = _gatheredOrderings[i];
                    _gatheredOrderings[i] =
                        new OrderExpression(
                            ord.OrderType == OrderType.Ascending ? OrderType.Descending : OrderType.Ascending,
                            ord.Expression
                            );
                }
            }
        }

        protected class BindResult
        {
            public BindResult(IEnumerable<ColumnDeclaration> columns, IEnumerable<OrderExpression> orderings)
            {
                Columns = columns as ReadOnlyCollection<ColumnDeclaration>;
                if (Columns == null)
                {
                    Columns = new List<ColumnDeclaration>(columns).AsReadOnly();
                }
                Orderings = orderings as ReadOnlyCollection<OrderExpression>;
                if (Orderings == null)
                {
                    Orderings = new List<OrderExpression>(orderings).AsReadOnly();
                }
            }
            public ReadOnlyCollection<ColumnDeclaration> Columns { get; }

            public ReadOnlyCollection<OrderExpression> Orderings { get; }
        }

        /// <summary>
        /// Rebind order expressions to reference a new alias and add to column declarations if necessary
        /// </summary>
        protected virtual BindResult RebindOrderings(IEnumerable<OrderExpression> orderings, TableAlias alias, HashSet<TableAlias> existingAliases, IEnumerable<ColumnDeclaration> existingColumns)
        {
            List<ColumnDeclaration> newColumns = null;
            var newOrderings = new List<OrderExpression>();
            foreach (var ordering in orderings)
            {
                var expr = ordering.Expression;
                var column = expr as ColumnExpression;
                if (column == null || (existingAliases != null && existingAliases.Contains(column.Alias)))
                {
                    // check to see if a declared column already contains a similar expression
                    var iOrdinal = 0;
                    foreach (var decl in existingColumns)
                    {
                        var declColumn = decl.Expression as ColumnExpression;
                        if (decl.Expression == ordering.Expression ||
                            (column != null && declColumn != null && column.Alias == declColumn.Alias && column.Name == declColumn.Name))
                        {
                            // found it, so make a reference to this column
                            expr = new ColumnExpression(column.Type, column.QueryType, alias, decl.Name);
                            break;
                        }
                        iOrdinal++;
                    }
                    // if not already projected, add a new column declaration for it
                    if (expr == ordering.Expression)
                    {
                        if (newColumns == null)
                        {
                            newColumns = new List<ColumnDeclaration>(existingColumns);
                            existingColumns = newColumns;
                        }
                        var colName = column != null ? column.Name : "c" + iOrdinal;
                        colName = newColumns.GetAvailableColumnName(colName);
                        var colType = _language.TypeSystem.GetColumnType(expr.Type);
                        newColumns.Add(new ColumnDeclaration(colName, ordering.Expression, colType));
                        expr = new ColumnExpression(expr.Type, colType, alias, colName);
                    }
                    newOrderings.Add(new OrderExpression(ordering.OrderType, expr));
                }
            }
            return new BindResult(existingColumns, newOrderings);
        }
    }
}
