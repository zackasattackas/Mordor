// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    /// <summary>
    /// rewrites nested projections into client-side joins
    /// </summary>
    public class ClientJoinedProjectionRewriter : DbExpressionVisitor
    {
        private readonly QueryPolicy _policy;
        private readonly QueryLanguage _language;
        private bool _isTopLevel = true;
        private SelectExpression _currentSelect;
        private MemberInfo _currentMember;
        private bool _canJoinOnClient = true;

        private ClientJoinedProjectionRewriter(QueryPolicy policy, QueryLanguage language)
        {
            _policy = policy;
            _language = language;
        }

        public static Expression Rewrite(QueryPolicy policy, QueryLanguage language, Expression expression)
        {
            return new ClientJoinedProjectionRewriter(policy, language).Visit(expression);
        }

        protected override MemberAssignment VisitMemberAssignment(MemberAssignment assignment)
        {
            var saveMember = _currentMember;
            _currentMember = assignment.Member;
            var e = Visit(assignment.Expression);
            _currentMember = saveMember;
            return UpdateMemberAssignment(assignment, assignment.Member, e);
        }

        protected override Expression VisitMemberAndExpression(MemberInfo member, Expression expression)
        {
            var saveMember = _currentMember;
            _currentMember = member;
            var e = Visit(expression);
            _currentMember = saveMember;
            return e;
        }

        protected override Expression VisitProjection(ProjectionExpression proj)
        {
            var save = _currentSelect;
            _currentSelect = proj.Select;
            try
            {
                if (!_isTopLevel)
                {
                    if (CanJoinOnClient(_currentSelect))
                    {
                        // make a query that combines all the constraints from the outer queries into a single select
                        var newOuterSelect = (SelectExpression)QueryDuplicator.Duplicate(save);

                        // remap any references to the outer select to the new alias;
                        var newInnerSelect = (SelectExpression)ColumnMapper.Map(proj.Select, newOuterSelect.Alias, save.Alias);
                        // add outer-join test
                        var newInnerProjection = _language.AddOuterJoinTest(new ProjectionExpression(newInnerSelect, proj.Projector));
                        newInnerSelect = newInnerProjection.Select;
                        var newProjector = newInnerProjection.Projector;

                        var newAlias = new TableAlias();
                        var pc = ColumnProjector.ProjectColumns(_language, newProjector, null, newAlias, newOuterSelect.Alias, newInnerSelect.Alias);

                        var join = new JoinExpression(JoinType.OuterApply, newOuterSelect, newInnerSelect, null);
                        var joinedSelect = new SelectExpression(newAlias, pc.Columns, join, null, null, null, proj.IsSingleton, null, null, false);

                        // apply client-join treatment recursively
                        _currentSelect = joinedSelect;
                        newProjector = Visit(pc.Projector);

                        // compute keys (this only works if join condition was a single column comparison)
                        var outerKeys = new List<Expression>();
                        var innerKeys = new List<Expression>();
                        if (GetEquiJoinKeyExpressions(newInnerSelect.Where, newOuterSelect.Alias, outerKeys, innerKeys))
                        {
                            // outerKey needs to refer to the outer-scope's alias
                            var outerKey = outerKeys.Select(k => ColumnMapper.Map(k, save.Alias, newOuterSelect.Alias));
                            // innerKey needs to refer to the new alias for the select with the new join
                            var innerKey = innerKeys.Select(k => ColumnMapper.Map(k, joinedSelect.Alias, ((ColumnExpression)k).Alias));
                            var newProjection = new ProjectionExpression(joinedSelect, newProjector, proj.Aggregator);
                            return new ClientJoinExpression(newProjection, outerKey, innerKey);
                        }
                    }
                    else
                    {
                        var saveJoin = _canJoinOnClient;
                        _canJoinOnClient = false;
                        var result = base.VisitProjection(proj);
                        _canJoinOnClient = saveJoin;
                        return result;
                    }
                }
                else
                {
                    _isTopLevel = false;
                }
                return base.VisitProjection(proj);
            }
            finally 
            {
                _currentSelect = save;
            }
        }

        private bool CanJoinOnClient(SelectExpression select)
        {
            // can add singleton (1:0,1) join if no grouping/aggregates or distinct
            return 
                _canJoinOnClient 
                && _currentMember != null 
                && !_policy.IsDeferLoaded(_currentMember)
                && !select.IsDistinct
                && (select.GroupBy == null || select.GroupBy.Count == 0)
                && !AggregateChecker.HasAggregates(select);
        }

        private bool GetEquiJoinKeyExpressions(Expression predicate, TableAlias outerAlias, List<Expression> outerExpressions, List<Expression> innerExpressions)
        {
            if (predicate.NodeType == ExpressionType.Equal)
            {
                var b = (BinaryExpression)predicate;
                var leftCol = GetColumnExpression(b.Left);
                var rightCol = GetColumnExpression(b.Right);
                if (leftCol != null && rightCol != null)
                {
                    if (leftCol.Alias == outerAlias)
                    {
                        outerExpressions.Add(b.Left);
                        innerExpressions.Add(b.Right);
                        return true;
                    }

                    if (rightCol.Alias == outerAlias)
                    {
                        innerExpressions.Add(b.Left);
                        outerExpressions.Add(b.Right);
                        return true;
                    }
                }
            }

            var hadKey = false;
            var parts = predicate.Split(ExpressionType.And, ExpressionType.AndAlso);
            if (parts.Length > 1)
            {
                foreach (var part in parts)
                {
                    var hasOuterAliasReference = ReferencedAliasGatherer.Gather(part).Contains(outerAlias);
                    if (hasOuterAliasReference)
                    {
                        if (!GetEquiJoinKeyExpressions(part, outerAlias, outerExpressions, innerExpressions))
                            return false;
                        hadKey = true;
                    }
                }
            }

            return hadKey;
        }

        private ColumnExpression GetColumnExpression(Expression expression)
        {
            // ignore converions 
            while (expression.NodeType == ExpressionType.Convert || expression.NodeType == ExpressionType.ConvertChecked)
            {
                expression = ((UnaryExpression)expression).Operand;
            }
            return expression as ColumnExpression;
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