// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Translation;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    /// <summary>
    /// Determines if two expressions are equivalent. Supports DbExpression nodes.
    /// </summary>
    public class DbExpressionComparer : ExpressionComparer
    {
        private ScopedDictionary<TableAlias, TableAlias> _aliasScope;

        protected DbExpressionComparer(
            ScopedDictionary<ParameterExpression, ParameterExpression> parameterScope, 
            Func<object, object, bool> fnCompare,
            ScopedDictionary<TableAlias, TableAlias> aliasScope)
            : base(parameterScope, fnCompare)
        {
            _aliasScope = aliasScope;
        }

        public new static bool AreEqual(Expression a, Expression b)
        {
            return AreEqual(null, null, a, b, null);
        }

        public new static bool AreEqual(Expression a, Expression b, Func<object, object, bool> fnCompare)
        {
            return AreEqual(null, null, a, b, fnCompare);
        }

        public static bool AreEqual(ScopedDictionary<ParameterExpression, ParameterExpression> parameterScope, ScopedDictionary<TableAlias, TableAlias> aliasScope, Expression a, Expression b)
        {
            return new DbExpressionComparer(parameterScope, null, aliasScope).Compare(a, b);
        }

        public static bool AreEqual(ScopedDictionary<ParameterExpression, ParameterExpression> parameterScope, ScopedDictionary<TableAlias, TableAlias> aliasScope, Expression a, Expression b, Func<object, object, bool> fnCompare)
        {
            return new DbExpressionComparer(parameterScope, fnCompare, aliasScope).Compare(a, b);
        }

        protected override bool Compare(Expression a, Expression b)
        {
            if (a == b)
                return true;
            if (a == null || b == null)
                return false;
            if (a.NodeType != b.NodeType)
                return false;
            if (a.Type != b.Type)
                return false;
            switch ((DbExpressionType)a.NodeType)
            {
                case DbExpressionType.Table:
                    return CompareTable((TableExpression)a, (TableExpression)b);
                case DbExpressionType.Column:
                    return CompareColumn((ColumnExpression)a, (ColumnExpression)b);
                case DbExpressionType.Select:
                    return CompareSelect((SelectExpression)a, (SelectExpression)b);
                case DbExpressionType.Join:
                    return CompareJoin((JoinExpression)a, (JoinExpression)b);
                case DbExpressionType.Aggregate:
                    return CompareAggregate((AggregateExpression)a, (AggregateExpression)b);
                case DbExpressionType.Scalar:
                case DbExpressionType.Exists:
                case DbExpressionType.In:
                    return CompareSubquery((SubqueryExpression)a, (SubqueryExpression)b);
                case DbExpressionType.AggregateSubquery:
                    return CompareAggregateSubquery((AggregateSubqueryExpression)a, (AggregateSubqueryExpression)b);
                case DbExpressionType.IsNull:
                    return CompareIsNull((IsNullExpression)a, (IsNullExpression)b);
                case DbExpressionType.Between:
                    return CompareBetween((BetweenExpression)a, (BetweenExpression)b);
                case DbExpressionType.RowCount:
                    return CompareRowNumber((RowNumberExpression)a, (RowNumberExpression)b);
                case DbExpressionType.Projection:
                    return CompareProjection((ProjectionExpression)a, (ProjectionExpression)b);
                case DbExpressionType.NamedValue:
                    return CompareNamedValue((NamedValueExpression)a, (NamedValueExpression)b);
                case DbExpressionType.Insert:
                    return CompareInsert((InsertCommand)a, (InsertCommand)b);
                case DbExpressionType.Update:
                    return CompareUpdate((UpdateCommand)a, (UpdateCommand)b);
                case DbExpressionType.Delete:
                    return CompareDelete((DeleteCommand)a, (DeleteCommand)b);
                case DbExpressionType.Batch:
                    return CompareBatch((BatchExpression)a, (BatchExpression)b);
                case DbExpressionType.Function:
                    return CompareFunction((FunctionExpression)a, (FunctionExpression)b);
                case DbExpressionType.Entity:
                    return CompareEntity((EntityExpression)a, (EntityExpression)b);
                case DbExpressionType.If:
                    return CompareIf((IfCommand)a, (IfCommand)b);
                case DbExpressionType.Block:
                    return CompareBlock((BlockCommand)a, (BlockCommand)b);
                default:
                    return base.Compare(a, b);
            }
        }

        protected virtual bool CompareTable(TableExpression a, TableExpression b)
        {
            return a.Name == b.Name;
        }

        protected virtual bool CompareColumn(ColumnExpression a, ColumnExpression b)
        {
            return CompareAlias(a.Alias, b.Alias) && a.Name == b.Name;
        }

        protected virtual bool CompareAlias(TableAlias a, TableAlias b)        
        {
            if (_aliasScope != null)
            {
                TableAlias mapped;
                if (_aliasScope.TryGetValue(a, out mapped))
                    return mapped == b;
            }
            return a == b;
        }

        protected virtual bool CompareSelect(SelectExpression a, SelectExpression b)
        {
            var save = _aliasScope;
            try
            {
                if (!Compare(a.From, b.From))
                    return false;

                _aliasScope = new ScopedDictionary<TableAlias, TableAlias>(save);
                MapAliases(a.From, b.From);

                return Compare(a.Where, b.Where)
                    && CompareOrderList(a.OrderBy, b.OrderBy)
                    && CompareExpressionList(a.GroupBy, b.GroupBy)
                    && Compare(a.Skip, b.Skip)
                    && Compare(a.Take, b.Take)
                    && a.IsDistinct == b.IsDistinct
                    && a.IsReverse == b.IsReverse
                    && CompareColumnDeclarations(a.Columns, b.Columns);
            }
            finally
            {
                _aliasScope = save;
            }
        }

        private void MapAliases(Expression a, Expression b)
        {
            var prodA = DeclaredAliasGatherer.Gather(a).ToArray();
            var prodB = DeclaredAliasGatherer.Gather(b).ToArray();
            for (int i = 0, n = prodA.Length; i < n; i++) 
            {
                _aliasScope.Add(prodA[i], prodB[i]);
            }
        }

        protected virtual bool CompareOrderList(ReadOnlyCollection<OrderExpression> a, ReadOnlyCollection<OrderExpression> b)
        {
            if (a == b)
                return true;
            if (a == null || b == null)
                return false;
            if (a.Count != b.Count)
                return false;
            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (a[i].OrderType != b[i].OrderType ||
                    !Compare(a[i].Expression, b[i].Expression))
                    return false;
            }
            return true;
        }

        protected virtual bool CompareColumnDeclarations(ReadOnlyCollection<ColumnDeclaration> a, ReadOnlyCollection<ColumnDeclaration> b)
        {
            if (a == b)
                return true;
            if (a == null || b == null)
                return false;
            if (a.Count != b.Count)
                return false;
            for (int i = 0, n = a.Count; i < n; i++)
            {
                if (!CompareColumnDeclaration(a[i], b[i]))
                    return false;
            }
            return true;
        }

        protected virtual bool CompareColumnDeclaration(ColumnDeclaration a, ColumnDeclaration b)
        {
            return a.Name == b.Name && Compare(a.Expression, b.Expression);
        }

        protected virtual bool CompareJoin(JoinExpression a, JoinExpression b)
        {
            if (a.Join != b.Join || !Compare(a.Left, b.Left))
                return false;

            if (a.Join == JoinType.CrossApply || a.Join == JoinType.OuterApply)
            {
                var save = _aliasScope;
                try
                {
                    _aliasScope = new ScopedDictionary<TableAlias, TableAlias>(_aliasScope);
                    MapAliases(a.Left, b.Left);

                    return Compare(a.Right, b.Right)
                        && Compare(a.Condition, b.Condition);
                }
                finally
                {
                    _aliasScope = save;
                }
            }

            return Compare(a.Right, b.Right)
                   && Compare(a.Condition, b.Condition);
        }

        protected virtual bool CompareAggregate(AggregateExpression a, AggregateExpression b)
        {
            return a.AggregateName == b.AggregateName && Compare(a.Argument, b.Argument);
        }

        protected virtual bool CompareIsNull(IsNullExpression a, IsNullExpression b)
        {
            return Compare(a.Expression, b.Expression);
        }

        protected virtual bool CompareBetween(BetweenExpression a, BetweenExpression b)
        {
            return Compare(a.Expression, b.Expression)
                && Compare(a.Lower, b.Lower)
                && Compare(a.Upper, b.Upper);
        }

        protected virtual bool CompareRowNumber(RowNumberExpression a, RowNumberExpression b)
        {
            return CompareOrderList(a.OrderBy, b.OrderBy);
        }

        protected virtual bool CompareNamedValue(NamedValueExpression a, NamedValueExpression b)
        {
            return a.Name == b.Name && Compare(a.Value, b.Value);
        }

        protected virtual bool CompareSubquery(SubqueryExpression a, SubqueryExpression b)
        {
            if (a.NodeType != b.NodeType)
                return false;
            switch ((DbExpressionType)a.NodeType)
            {
                case DbExpressionType.Scalar:
                    return CompareScalar((ScalarExpression)a, (ScalarExpression)b);
                case DbExpressionType.Exists:
                    return CompareExists((ExistsExpression)a, (ExistsExpression)b);
                case DbExpressionType.In:
                    return CompareIn((InExpression)a, (InExpression)b);
            }
            return false;
        }

        protected virtual bool CompareScalar(ScalarExpression a, ScalarExpression b)
        {
            return Compare(a.Select, b.Select);
        }

        protected virtual bool CompareExists(ExistsExpression a, ExistsExpression b)
        {
            return Compare(a.Select, b.Select);
        }

        protected virtual bool CompareIn(InExpression a, InExpression b)
        {
            return Compare(a.Expression, b.Expression)
                && Compare(a.Select, b.Select)
                && CompareExpressionList(a.Values, b.Values);
        }

        protected virtual bool CompareAggregateSubquery(AggregateSubqueryExpression a, AggregateSubqueryExpression b)
        {
            return Compare(a.AggregateAsSubquery, b.AggregateAsSubquery)
                && Compare(a.AggregateInGroupSelect, b.AggregateInGroupSelect)
                && a.GroupByAlias == b.GroupByAlias;
        }

        protected virtual bool CompareProjection(ProjectionExpression a, ProjectionExpression b)
        {
            if (!Compare(a.Select, b.Select))
                return false;

            var save = _aliasScope;
            try
            {
                _aliasScope = new ScopedDictionary<TableAlias, TableAlias>(_aliasScope);
                _aliasScope.Add(a.Select.Alias, b.Select.Alias);

                return Compare(a.Projector, b.Projector)
                    && Compare(a.Aggregator, b.Aggregator)
                    && a.IsSingleton == b.IsSingleton;
            }
            finally
            {
                _aliasScope = save;
            }
        }

        protected virtual bool CompareInsert(InsertCommand x, InsertCommand y)
        {
            return Compare(x.Table, y.Table)
                && CompareColumnAssignments(x.Assignments, y.Assignments);
        }

        protected virtual bool CompareColumnAssignments(ReadOnlyCollection<ColumnAssignment> x, ReadOnlyCollection<ColumnAssignment> y)
        {
            if (x == y)
                return true;
            if (x.Count != y.Count)
                return false;
            for (int i = 0, n = x.Count; i < n; i++)
            {
                if (!Compare(x[i].Column, y[i].Column) || !Compare(x[i].Expression, y[i].Expression))
                    return false;
            }
            return true;
        }

        protected virtual bool CompareUpdate(UpdateCommand x, UpdateCommand y)
        {
            return Compare(x.Table, y.Table) && Compare(x.Where, y.Where) && CompareColumnAssignments(x.Assignments, y.Assignments);
        }

        protected virtual bool CompareDelete(DeleteCommand x, DeleteCommand y)
        {
            return Compare(x.Table, y.Table) && Compare(x.Where, y.Where);
        }

        protected virtual bool CompareBatch(BatchExpression x, BatchExpression y)
        {
            return Compare(x.Input, y.Input) && Compare(x.Operation, y.Operation)
                && Compare(x.BatchSize, y.BatchSize) && Compare(x.Stream, y.Stream);
        }

        protected virtual bool CompareIf(IfCommand x, IfCommand y)
        {
            return Compare(x.Check, y.Check) && Compare(x.IfTrue, y.IfTrue) && Compare(x.IfFalse, y.IfFalse);
        }

        protected virtual bool CompareBlock(BlockCommand x, BlockCommand y)
        {
            if (x.Commands.Count != y.Commands.Count)
                return false;
            for (int i = 0, n = x.Commands.Count; i < n; i++)
            {
                if (!Compare(x.Commands[i], y.Commands[i]))
                    return false;
            }
            return true;
        }

        protected virtual bool CompareFunction(FunctionExpression x, FunctionExpression y)
        {
            return x.Name == y.Name && CompareExpressionList(x.Arguments, y.Arguments);
        }

        protected virtual bool CompareEntity(EntityExpression x, EntityExpression y)
        {
            return x.Entity == y.Entity && Compare(x.Expression, y.Expression);
        }
    }
}