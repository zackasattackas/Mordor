// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    /// <summary>
    /// Rewrites nested singleton projection into server-side joins
    /// </summary>
    public class SingletonProjectionRewriter : DbExpressionVisitor
    {
        private readonly QueryLanguage _language;
        private bool _isTopLevel = true;
        private SelectExpression _currentSelect;

        private SingletonProjectionRewriter(QueryLanguage language)
        {
            _language = language;
        }

        public static Expression Rewrite(QueryLanguage language, Expression expression)
        {
            return new SingletonProjectionRewriter(language).Visit(expression);
        }

        protected override Expression VisitClientJoin(ClientJoinExpression join)
        {
            // treat client joins as new top level
            var saveTop = _isTopLevel;
            var saveSelect = _currentSelect;
            _isTopLevel = true;
            _currentSelect = null;
            var result = base.VisitClientJoin(join);
            _isTopLevel = saveTop;
            _currentSelect = saveSelect;
            return result;
        }

        protected override Expression VisitProjection(ProjectionExpression proj)
        {
            if (_isTopLevel)
            {
                _isTopLevel = false;
                _currentSelect = proj.Select;
                var projector = Visit(proj.Projector);
                if (projector != proj.Projector || _currentSelect != proj.Select)
                {
                    return new ProjectionExpression(_currentSelect, projector, proj.Aggregator);
                }
                return proj;
            }

            if (proj.IsSingleton && CanJoinOnServer(_currentSelect))
            {
                var newAlias = new TableAlias();
                _currentSelect = _currentSelect.AddRedundantSelect(_language, newAlias);

                // remap any references to the outer select to the new alias;
                var source =(SelectExpression)ColumnMapper.Map(proj.Select, newAlias, _currentSelect.Alias);

                // add outer-join test
                var pex = _language.AddOuterJoinTest(new ProjectionExpression(source, proj.Projector));

                var pc = ColumnProjector.ProjectColumns(_language, pex.Projector, _currentSelect.Columns, _currentSelect.Alias, newAlias, proj.Select.Alias);

                var join = new JoinExpression(JoinType.OuterApply, _currentSelect.From, pex.Select, null);

                _currentSelect = new SelectExpression(_currentSelect.Alias, pc.Columns, join, null);
                return Visit(pc.Projector);
            }

            var saveTop = _isTopLevel;
            var saveSelect = _currentSelect;
            _isTopLevel = true;
            _currentSelect = null;
            var result = base.VisitProjection(proj);
            _isTopLevel = saveTop;
            _currentSelect = saveSelect;
            return result;
        }

        private bool CanJoinOnServer(SelectExpression select)
        {
            // can add singleton (1:0,1) join if no grouping/aggregates or distinct
            return !select.IsDistinct
                && (select.GroupBy == null || select.GroupBy.Count == 0)
                && !AggregateChecker.HasAggregates(select);
        }

        protected override Expression VisitSubquery(SubqueryExpression subquery)
        {
            return subquery;
        }

        protected override Expression VisitCommand(CommandExpression command)
        {
            _isTopLevel = true;
            return base.VisitCommand(command);
        }
    }
}