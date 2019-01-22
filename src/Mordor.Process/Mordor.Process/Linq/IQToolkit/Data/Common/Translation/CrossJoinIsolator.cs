// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    /// <summary>
    /// Isolates cross joins from other types of joins using nested sub queries
    /// </summary>
    public class CrossJoinIsolator : DbExpressionVisitor
    {
        private ILookup<TableAlias, ColumnExpression> _columns;
        private readonly Dictionary<ColumnExpression, ColumnExpression> _map = new Dictionary<ColumnExpression,ColumnExpression>();
        private JoinType? _lastJoin;

        public static Expression Isolate(Expression expression)
        {
            return new CrossJoinIsolator().Visit(expression);
        }

        protected override Expression VisitSelect(SelectExpression select)
        {
            var saveColumns = _columns;
            _columns = ReferencedColumnGatherer.Gather(select).ToLookup(c => c.Alias);
            var saveLastJoin = _lastJoin;
            _lastJoin = null;
            var result = base.VisitSelect(select);
            _columns = saveColumns;
            _lastJoin = saveLastJoin;
            return result;
        }

        protected override Expression VisitJoin(JoinExpression join)
        {
            var saveLastJoin = _lastJoin;
            _lastJoin = join.Join;
            join = (JoinExpression)base.VisitJoin(join);
            _lastJoin = saveLastJoin;

            if (_lastJoin != null && (join.Join == JoinType.CrossJoin) != (_lastJoin == JoinType.CrossJoin))
            {
                var result = MakeSubquery(join);
                return result;
            }
            return join;
        }

        private bool IsCrossJoin(Expression expression)
        {
            var jex = expression as JoinExpression;
            if (jex != null)
            {
                return jex.Join == JoinType.CrossJoin;
            }
            return false;
        }

        private Expression MakeSubquery(Expression expression)
        {
            var newAlias = new TableAlias();
            var aliases = DeclaredAliasGatherer.Gather(expression);

            var decls = new List<ColumnDeclaration>();
            foreach (var ta in aliases) 
            {
                foreach (var col in _columns[ta])
                {
                    var name = decls.GetAvailableColumnName(col.Name);
                    var decl = new ColumnDeclaration(name, col, col.QueryType);
                    decls.Add(decl);
                    var newCol = new ColumnExpression(col.Type, col.QueryType, newAlias, col.Name);
                    _map.Add(col, newCol);
                }
            }

            return new SelectExpression(newAlias, decls, expression, null);
        }

        protected override Expression VisitColumn(ColumnExpression column)
        {
            ColumnExpression mapped;
            if (_map.TryGetValue(column, out mapped))
            {
                return mapped;
            }
            return column;
        }
    }
}