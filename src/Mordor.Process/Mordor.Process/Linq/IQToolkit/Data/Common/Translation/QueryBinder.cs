﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;
using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    /// <summary>
    /// Converts LINQ query operators to into custom DbExpression's
    /// </summary>
    public class QueryBinder : DbExpressionVisitor
    {
        private readonly QueryMapper _mapper;
        private readonly QueryLanguage _language;
        private readonly Dictionary<ParameterExpression, Expression> _map;
        private readonly Dictionary<Expression, GroupByInfo> _groupByMap;
        private Expression _root;
        private IEntityTable _batchUpd;

        private QueryBinder(QueryMapper mapper, Expression root)
        {
            _mapper = mapper;
            _language = mapper.Translator.Linguist.Language;
            _map = new Dictionary<ParameterExpression, Expression>();
            _groupByMap = new Dictionary<Expression, GroupByInfo>();
            _root = root;
        }

        public static Expression Bind(QueryMapper mapper, Expression expression)
        {
            return new QueryBinder(mapper, expression).Visit(expression);
        }

        private static LambdaExpression GetLambda(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote)
            {
                e = ((UnaryExpression)e).Operand;
            }
            if (e.NodeType == ExpressionType.Constant)
            {
                return ((ConstantExpression)e).Value as LambdaExpression;
            }
            return e as LambdaExpression;
        }

        internal TableAlias GetNextAlias()
        {
            return new TableAlias();
        }

        private ProjectedColumns ProjectColumns(Expression expression, TableAlias newAlias, params TableAlias[] existingAliases)
        {
            return ColumnProjector.ProjectColumns(_language, expression, null, newAlias, existingAliases);
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            if (m.Method.DeclaringType == typeof(Queryable) || m.Method.DeclaringType == typeof(Enumerable))
            {
                switch (m.Method.Name)
                {
                    case "Where":
                        return BindWhere(m.Type, m.Arguments[0], GetLambda(m.Arguments[1]));
                    case "Select":
                        return BindSelect(m.Type, m.Arguments[0], GetLambda(m.Arguments[1]));
                    case "SelectMany":
                        if (m.Arguments.Count == 2)
                        {
                            return BindSelectMany(m.Type, m.Arguments[0], GetLambda(m.Arguments[1]), null);
                        }
                        else if (m.Arguments.Count == 3)
                        {
                            return BindSelectMany(m.Type, m.Arguments[0], GetLambda(m.Arguments[1]), GetLambda(m.Arguments[2]));
                        }
                        break;
                    case "Join":
                        return BindJoin(m.Type, m.Arguments[0], m.Arguments[1], GetLambda(m.Arguments[2]), GetLambda(m.Arguments[3]), GetLambda(m.Arguments[4]));
                    case "GroupJoin":
                        if (m.Arguments.Count == 5)
                        {
                            return BindGroupJoin(m.Method, m.Arguments[0], m.Arguments[1], GetLambda(m.Arguments[2]), GetLambda(m.Arguments[3]), GetLambda(m.Arguments[4]));
                        }
                        break;
                    case "OrderBy":
                        return BindOrderBy(m.Type, m.Arguments[0], GetLambda(m.Arguments[1]), OrderType.Ascending);
                    case "OrderByDescending":
                        return BindOrderBy(m.Type, m.Arguments[0], GetLambda(m.Arguments[1]), OrderType.Descending);
                    case "ThenBy":
                        return BindThenBy(m.Arguments[0], GetLambda(m.Arguments[1]), OrderType.Ascending);
                    case "ThenByDescending":
                        return BindThenBy(m.Arguments[0], GetLambda(m.Arguments[1]), OrderType.Descending);
                    case "GroupBy":
                        if (m.Arguments.Count == 2)
                        {
                            return BindGroupBy(m.Arguments[0], GetLambda(m.Arguments[1]), null, null);
                        }
                        else if (m.Arguments.Count == 3)
                        {
                            var lambda1 = GetLambda(m.Arguments[1]);
                            var lambda2 = GetLambda(m.Arguments[2]);
                            if (lambda2.Parameters.Count == 1)
                            {
                                // second lambda is element selector
                                return BindGroupBy(m.Arguments[0], lambda1, lambda2, null);
                            }

                            if (lambda2.Parameters.Count == 2)
                            {
                                // second lambda is result selector
                                return BindGroupBy(m.Arguments[0], lambda1, null, lambda2);
                            }
                        }
                        else if (m.Arguments.Count == 4)
                        {
                            return BindGroupBy(m.Arguments[0], GetLambda(m.Arguments[1]), GetLambda(m.Arguments[2]), GetLambda(m.Arguments[3]));
                        }
                        break;
                    case "Distinct":
                        if (m.Arguments.Count == 1)
                        {
                            return BindDistinct(m.Arguments[0]);
                        }
                        break;
                    case "Skip":
                        if (m.Arguments.Count == 2)
                        {
                            return BindSkip(m.Arguments[0], m.Arguments[1]);
                        }
                        break;
                    case "Take":
                        if (m.Arguments.Count == 2)
                        {
                            return BindTake(m.Arguments[0], m.Arguments[1]);
                        }
                        break;
                    case "First":
                    case "FirstOrDefault":
                    case "Single":
                    case "SingleOrDefault":
                    case "Last":
                    case "LastOrDefault":
                        if (m.Arguments.Count == 1)
                        {
                            return BindFirst(m.Arguments[0], null, m.Method.Name, m == _root);
                        }
                        else if (m.Arguments.Count == 2)
                        {
                            return BindFirst(m.Arguments[0], GetLambda(m.Arguments[1]), m.Method.Name, m == _root);
                        }
                        break;
                    case "Any":
                        if (m.Arguments.Count == 1)
                        {
                            return BindAnyAll(m.Arguments[0], m.Method, null, m == _root);
                        }
                        else if (m.Arguments.Count == 2)
                        {
                            return BindAnyAll(m.Arguments[0], m.Method, GetLambda(m.Arguments[1]), m == _root);
                        }
                        break;
                    case "All":
                        if (m.Arguments.Count == 2)
                        {
                            return BindAnyAll(m.Arguments[0], m.Method, GetLambda(m.Arguments[1]), m == _root);
                        }
                        break;
                    case "Contains":
                        if (m.Arguments.Count == 2)
                        {
                            return BindContains(m.Arguments[0], m.Arguments[1], m == _root);
                        }
                        break;
                    case "Cast":
                        if (m.Arguments.Count == 1)
                        {
                            return BindCast(m.Arguments[0], m.Method.GetGenericArguments()[0]);
                        }
                        break;
                    case "Reverse":
                        return BindReverse(m.Arguments[0]);
                    case "Intersect":
                    case "Except":
                        if (m.Arguments.Count == 2)
                        {
                            return BindIntersect(m.Arguments[0], m.Arguments[1], m.Method.Name == "Except");
                        }
                        break;
                }
            }
            else if (typeof(Updatable).IsAssignableFrom(m.Method.DeclaringType)) 
            {
                var upd = _batchUpd != null
                    ? _batchUpd
                    : (IEntityTable)((ConstantExpression)m.Arguments[0]).Value;

                switch (m.Method.Name)
                {
                    case "Insert":
                        return BindInsert(
                            upd,
                            m.Arguments[1], 
                            m.Arguments.Count > 2 ? GetLambda(m.Arguments[2]) : null
                            );
                    case "Update":
                        return BindUpdate(
                            upd,
                            m.Arguments[1], 
                            m.Arguments.Count > 2 ? GetLambda(m.Arguments[2]) : null, 
                            m.Arguments.Count > 3 ? GetLambda(m.Arguments[3]) : null
                            );
                    case "InsertOrUpdate":
                        return BindInsertOrUpdate(
                            upd,
                            m.Arguments[1],
                            m.Arguments.Count > 2 ? GetLambda(m.Arguments[2]) : null,
                            m.Arguments.Count > 3 ? GetLambda(m.Arguments[3]) : null
                            );
                    case "Delete":
                        if (m.Arguments.Count == 2 && GetLambda(m.Arguments[1]) != null)
                        {
                            return BindDelete(upd, null, GetLambda(m.Arguments[1]));
                        }
                        return BindDelete(
                            upd,
                            m.Arguments[1], 
                            m.Arguments.Count > 2 ? GetLambda(m.Arguments[2]) : null
                            );
                    case "Batch":
                        return BindBatch(
                            upd,
                            m.Arguments[1],
                            GetLambda(m.Arguments[2]),
                            m.Arguments.Count > 3 ? m.Arguments[3] : Expression.Constant(50),
                            m.Arguments.Count > 4 ? m.Arguments[4] : Expression.Constant(false)
                            );
                }
            }
            if (_language.IsAggregate(m.Method))
            {
                return BindAggregate(
                    m.Arguments[0], 
                    m.Method.Name, 
                    m.Method.ReturnType, 
                    m.Arguments.Count > 1 ? GetLambda(m.Arguments[1]) : null, 
                    m == _root
                    );
            }
            return base.VisitMethodCall(m);
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            if ((u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked)
                && u == _root)
            {
                _root = u.Operand;
            }
            return base.VisitUnary(u);
        }

        private ProjectionExpression VisitSequence(Expression source)
        {
            // sure to call base.Visit in order to skip my override
            return ConvertToSequence(base.Visit(source));
        }

        private ProjectionExpression ConvertToSequence(Expression expr)
        {
            switch (expr.NodeType)
            {
                case (ExpressionType)DbExpressionType.Projection:
                    return (ProjectionExpression)expr;
                case ExpressionType.New:
                    var nex = (NewExpression)expr;
                    if (expr.Type.IsGenericType && expr.Type.GetGenericTypeDefinition() == typeof(Grouping<,>))
                    {
                        return (ProjectionExpression)nex.Arguments[1];
                    }
                    goto default;
                case ExpressionType.MemberAccess:
                    var bound = BindRelationshipProperty((MemberExpression)expr);
                    if (bound.NodeType != ExpressionType.MemberAccess)
                        return ConvertToSequence(bound);
                    goto default;
                default:
                    var n = GetNewExpression(expr);
                    if (n != null)
                    {
                        expr = n;
                        goto case ExpressionType.New;
                    }
                    throw new Exception(string.Format("The expression of type '{0}' is not a sequence", expr.Type));
            }
        }

        private Expression BindRelationshipProperty(MemberExpression mex)
        {
            var ex = mex.Expression as EntityExpression;
            if (ex != null && _mapper.Mapping.IsRelationship(ex.Entity, mex.Member))
            {
                return _mapper.GetMemberExpression(mex.Expression, ex.Entity, mex.Member);
            }
            return mex;
        }

        protected override Expression Visit(Expression exp)
        {
            var result = base.Visit(exp);

            if (result != null)
            {
                // bindings that expect projections should have called VisitSequence, the rest will probably get annoyed if
                // the projection does not have the expected type.
                var expectedType = exp.Type;
                var projection = result as ProjectionExpression;
                if (projection != null && projection.Aggregator == null && !expectedType.IsAssignableFrom(projection.Type))
                {
                    var aggregator = Aggregator.GetAggregator(expectedType, projection.Type);
                    if (aggregator != null)
                    {
                        return new ProjectionExpression(projection.Select, projection.Projector, aggregator);
                    }
                }
            }

            return result;
        }

        private Expression BindWhere(Type resultType, Expression source, LambdaExpression predicate)
        {
            var projection = VisitSequence(source);
            _map[predicate.Parameters[0]] = projection.Projector;
            var where = Visit(predicate.Body);
            var alias = GetNextAlias();
            var pc = ProjectColumns(projection.Projector, alias, projection.Select.Alias);
            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, projection.Select, where),
                pc.Projector
                );
        }

        private Expression BindReverse(Expression source)
        {
            var projection = VisitSequence(source);
            var alias = GetNextAlias();
            var pc = ProjectColumns(projection.Projector, alias, projection.Select.Alias);
            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, projection.Select, null).SetReverse(true),
                pc.Projector
                );
        }

        private Expression BindSelect(Type resultType, Expression source, LambdaExpression selector)
        {
            var projection = VisitSequence(source);
            _map[selector.Parameters[0]] = projection.Projector;
            var expression = Visit(selector.Body);
            var alias = GetNextAlias();
            var pc = ProjectColumns(expression, alias, projection.Select.Alias);
            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, projection.Select, null),
                pc.Projector
                );
        }

        protected virtual Expression BindSelectMany(Type resultType, Expression source, LambdaExpression collectionSelector, LambdaExpression resultSelector)
        {
            var projection = VisitSequence(source);
            _map[collectionSelector.Parameters[0]] = projection.Projector;

            var collection = collectionSelector.Body;

            // check for DefaultIfEmpty
            var defaultIfEmpty = false;
            var mcs = collection as MethodCallExpression;
            if (mcs != null && mcs.Method.Name == "DefaultIfEmpty" && mcs.Arguments.Count == 1 &&
                (mcs.Method.DeclaringType == typeof(Queryable) || mcs.Method.DeclaringType == typeof(Enumerable)))
            {
                collection = mcs.Arguments[0];
                defaultIfEmpty = true;
            }

            var collectionProjection = VisitSequence(collection);
            var isTable = collectionProjection.Select.From is TableExpression;
            var joinType = isTable ? JoinType.CrossJoin : defaultIfEmpty ? JoinType.OuterApply : JoinType.CrossApply;
            if (joinType == JoinType.OuterApply)
            {
                collectionProjection = _language.AddOuterJoinTest(collectionProjection);
            }
            var join = new JoinExpression(joinType, projection.Select, collectionProjection.Select, null);

            var alias = GetNextAlias();
            ProjectedColumns pc;
            if (resultSelector == null)
            {
                pc = ProjectColumns(collectionProjection.Projector, alias, projection.Select.Alias, collectionProjection.Select.Alias);
            }
            else
            {
                _map[resultSelector.Parameters[0]] = projection.Projector;
                _map[resultSelector.Parameters[1]] = collectionProjection.Projector;
                var result = Visit(resultSelector.Body);
                pc = ProjectColumns(result, alias, projection.Select.Alias, collectionProjection.Select.Alias);
            }
            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, join, null),
                pc.Projector
                );
        }

        protected virtual Expression BindJoin(Type resultType, Expression outerSource, Expression innerSource, LambdaExpression outerKey, LambdaExpression innerKey, LambdaExpression resultSelector)
        {
            var outerProjection = VisitSequence(outerSource);
            var innerProjection = VisitSequence(innerSource);
            _map[outerKey.Parameters[0]] = outerProjection.Projector;
            var outerKeyExpr = Visit(outerKey.Body);
            _map[innerKey.Parameters[0]] = innerProjection.Projector;
            var innerKeyExpr = Visit(innerKey.Body);
            _map[resultSelector.Parameters[0]] = outerProjection.Projector;
            _map[resultSelector.Parameters[1]] = innerProjection.Projector;
            var resultExpr = Visit(resultSelector.Body);
            var join = new JoinExpression(JoinType.InnerJoin, outerProjection.Select, innerProjection.Select, outerKeyExpr.Equal(innerKeyExpr));
            var alias = GetNextAlias();
            var pc = ProjectColumns(resultExpr, alias, outerProjection.Select.Alias, innerProjection.Select.Alias);
            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, join, null),
                pc.Projector
                );
        }

        protected virtual Expression BindIntersect(Expression outerSource, Expression innerSource, bool negate)
        {
            // SELECT * FROM outer WHERE EXISTS(SELECT * FROM inner WHERE inner = outer))
            var outerProjection = VisitSequence(outerSource);
            var innerProjection = VisitSequence(innerSource);

            Expression exists = new ExistsExpression(
                new SelectExpression(new TableAlias(), null, innerProjection.Select, innerProjection.Projector.Equal(outerProjection.Projector))
                );
            if (negate)
                exists = Expression.Not(exists);
            var alias = GetNextAlias();
            var pc = ProjectColumns(outerProjection.Projector, alias, outerProjection.Select.Alias);
            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, outerProjection.Select, exists),
                pc.Projector, outerProjection.Aggregator
                );
        }

        protected virtual Expression BindGroupJoin(MethodInfo groupJoinMethod, Expression outerSource, Expression innerSource, LambdaExpression outerKey, LambdaExpression innerKey, LambdaExpression resultSelector)
        {
            // A database will treat this no differently than a SelectMany w/ result selector, so just use that translation instead
            var args = groupJoinMethod.GetGenericArguments();

            var outerProjection = VisitSequence(outerSource);

            _map[outerKey.Parameters[0]] = outerProjection.Projector;
            var predicateLambda = Expression.Lambda(innerKey.Body.Equal(outerKey.Body), innerKey.Parameters[0]);
            var callToWhere = Expression.Call(typeof(Enumerable), "Where", new[] { args[1] }, innerSource, predicateLambda);           
            var group = Visit(callToWhere);

            _map[resultSelector.Parameters[0]] = outerProjection.Projector;
            _map[resultSelector.Parameters[1]] = group;
            var resultExpr = Visit(resultSelector.Body);

            var alias = GetNextAlias();
            var pc = ProjectColumns(resultExpr, alias, outerProjection.Select.Alias);
            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, outerProjection.Select, null),
                pc.Projector
                );
        }

        private List<OrderExpression> _thenBys;

        protected virtual Expression BindOrderBy(Type resultType, Expression source, LambdaExpression orderSelector, OrderType orderType)
        {
            var myThenBys = _thenBys;
            _thenBys = null;
            var projection = VisitSequence(source);

            _map[orderSelector.Parameters[0]] = projection.Projector;
            var orderings = new List<OrderExpression>();
            orderings.Add(new OrderExpression(orderType, Visit(orderSelector.Body)));

            if (myThenBys != null)
            {
                for (var i = myThenBys.Count - 1; i >= 0; i--)
                {
                    var tb = myThenBys[i];
                    var lambda = (LambdaExpression)tb.Expression;
                    _map[lambda.Parameters[0]] = projection.Projector;
                    orderings.Add(new OrderExpression(tb.OrderType, Visit(lambda.Body)));
                }
            }

            var alias = GetNextAlias();
            var pc = ProjectColumns(projection.Projector, alias, projection.Select.Alias);
            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, projection.Select, null, orderings.AsReadOnly(), null),
                pc.Projector
                );
        }

        protected virtual Expression BindThenBy(Expression source, LambdaExpression orderSelector, OrderType orderType)
        {
            if (_thenBys == null)
            {
                _thenBys = new List<OrderExpression>();
            }
            _thenBys.Add(new OrderExpression(orderType, orderSelector));
            return Visit(source);
        }

        protected virtual Expression BindGroupBy(Expression source, LambdaExpression keySelector, LambdaExpression elementSelector, LambdaExpression resultSelector)
        {
            var projection = VisitSequence(source);

            _map[keySelector.Parameters[0]] = projection.Projector;
            var keyExpr = Visit(keySelector.Body);

            var elemExpr = projection.Projector;
            if (elementSelector != null)
            {
                _map[elementSelector.Parameters[0]] = projection.Projector;
                elemExpr = Visit(elementSelector.Body);
            }

            // Use ProjectColumns to get group-by expressions from key expression
            var keyProjection = ProjectColumns(keyExpr, projection.Select.Alias, projection.Select.Alias);
            var groupExprs = keyProjection.Columns.Select(c => c.Expression).ToArray();

            // make duplicate of source query as basis of element subquery by visiting the source again
            var subqueryBasis = VisitSequence(source);

            // recompute key columns for group expressions relative to subquery (need these for doing the correlation predicate)
            _map[keySelector.Parameters[0]] = subqueryBasis.Projector;
            var subqueryKey = Visit(keySelector.Body);

            // use same projection trick to get group-by expressions based on subquery
            var subqueryKeyPc = ProjectColumns(subqueryKey, subqueryBasis.Select.Alias, subqueryBasis.Select.Alias);
            var subqueryGroupExprs = subqueryKeyPc.Columns.Select(c => c.Expression).ToArray();
            var subqueryCorrelation = BuildPredicateWithNullsEqual(subqueryGroupExprs, groupExprs);

            // compute element based on duplicated subquery
            var subqueryElemExpr = subqueryBasis.Projector;
            if (elementSelector != null)
            {
                _map[elementSelector.Parameters[0]] = subqueryBasis.Projector;
                subqueryElemExpr = Visit(elementSelector.Body);
            }

            // build subquery that projects the desired element
            var elementAlias = GetNextAlias();
            var elementPc = ProjectColumns(subqueryElemExpr, elementAlias, subqueryBasis.Select.Alias);
            var elementSubquery =
                new ProjectionExpression(
                    new SelectExpression(elementAlias, elementPc.Columns, subqueryBasis.Select, subqueryCorrelation),
                    elementPc.Projector
                    );

            var alias = GetNextAlias();

            // make it possible to tie aggregates back to this group-by
            var info = new GroupByInfo(alias, elemExpr);
            _groupByMap.Add(elementSubquery, info);

            Expression resultExpr;
            if (resultSelector != null)
            {
                var saveGroupElement = _currentGroupElement;
                _currentGroupElement = elementSubquery;
                // compute result expression based on key & element-subquery
                _map[resultSelector.Parameters[0]] = keyProjection.Projector;
                _map[resultSelector.Parameters[1]] = elementSubquery;
                resultExpr = Visit(resultSelector.Body);
                _currentGroupElement = saveGroupElement;
            }
            else
            {
                // result must be IGrouping<K,E>
                resultExpr = 
                    Expression.New(
                        typeof(Grouping<,>).MakeGenericType(keyExpr.Type, subqueryElemExpr.Type).GetConstructors()[0], keyExpr, elementSubquery);

                resultExpr = Expression.Convert(resultExpr, typeof(IGrouping<,>).MakeGenericType(keyExpr.Type, subqueryElemExpr.Type));
            }

            var pc = ProjectColumns(resultExpr, alias, projection.Select.Alias);

            // make it possible to tie aggregates back to this group-by
            var newResult = GetNewExpression(pc.Projector);
            if (newResult != null && newResult.Type.IsGenericType && newResult.Type.GetGenericTypeDefinition() == typeof(Grouping<,>))
            {
                var projectedElementSubquery = newResult.Arguments[1];
                _groupByMap.Add(projectedElementSubquery, info);
            }

            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, projection.Select, null, null, groupExprs),
                pc.Projector
                );
        }

        private NewExpression GetNewExpression(Expression expression)
        {
            // ignore converions 
            while (expression.NodeType == ExpressionType.Convert || expression.NodeType == ExpressionType.ConvertChecked)
            {
                expression = ((UnaryExpression)expression).Operand;
            }
            return expression as NewExpression;
        }

        private Expression BuildPredicateWithNullsEqual(IEnumerable<Expression> source1, IEnumerable<Expression> source2)
        {
            var en1 = source1.GetEnumerator();
            var en2 = source2.GetEnumerator();
            Expression result = null;
            while (en1.MoveNext() && en2.MoveNext())
            {
                Expression compare =
                    Expression.Or(
                        new IsNullExpression(en1.Current).And(new IsNullExpression(en2.Current)),
                        en1.Current.Equal(en2.Current)
                        );
                result = (result == null) ? compare : result.And(compare);
            }
            return result;
        }

        private Expression _currentGroupElement;

        private class GroupByInfo
        {
            internal TableAlias Alias { get; private set; }
            internal Expression Element { get; private set; }
            internal GroupByInfo(TableAlias alias, Expression element)
            {
                Alias = alias;
                Element = element;
            }
        }

        private Expression BindAggregate(Expression source, string aggName, Type returnType, LambdaExpression argument, bool isRoot)
        {
            var hasPredicateArg = _language.AggregateArgumentIsPredicate(aggName);
            var isDistinct = false;
            var argumentWasPredicate = false;
            var useAlternateArg = false;

            // check for distinct
            var mcs = source as MethodCallExpression;
            if (mcs != null && !hasPredicateArg && argument == null)
            {
                if (mcs.Method.Name == "Distinct" && mcs.Arguments.Count == 1 &&
                    (mcs.Method.DeclaringType == typeof(Queryable) || mcs.Method.DeclaringType == typeof(Enumerable))
                    && _language.AllowDistinctInAggregates)
                {
                    source = mcs.Arguments[0];
                    isDistinct = true;
                }
            }

            if (argument != null && hasPredicateArg)
            {
                // convert query.Count(predicate) into query.Where(predicate).Count()
                source = Expression.Call(typeof(Queryable), "Where", new [] {TypeHelper.GetElementType(source.Type)}, source, argument);
                argument = null;
                argumentWasPredicate = true;
            }

            var projection = VisitSequence(source);

            Expression argExpr = null;
            if (argument != null)
            {
                _map[argument.Parameters[0]] = projection.Projector;
                argExpr = Visit(argument.Body);
            }
            else if (!hasPredicateArg || useAlternateArg)
            {
                argExpr = projection.Projector;
            }

            var alias = GetNextAlias();
            var pc = ProjectColumns(projection.Projector, alias, projection.Select.Alias);
            Expression aggExpr = new AggregateExpression(returnType, aggName, argExpr, isDistinct);
            var colType = _language.TypeSystem.GetColumnType(returnType);
            var select = new SelectExpression(alias, new[] { new ColumnDeclaration("", aggExpr, colType) }, projection.Select, null);

            if (isRoot)
            {
                var p = Expression.Parameter(typeof(IEnumerable<>).MakeGenericType(aggExpr.Type), "p");
                var gator = Expression.Lambda(Expression.Call(typeof(Enumerable), "Single", new[] { returnType }, p), p);
                return new ProjectionExpression(select, new ColumnExpression(returnType, _language.TypeSystem.GetColumnType(returnType), alias, ""), gator);
            }

            var subquery = new ScalarExpression(returnType, select);

            // if we can find the corresponding group-info we can build a special AggregateSubquery node that will enable us to 
            // optimize the aggregate expression later using AggregateRewriter
            GroupByInfo info;
            if (!argumentWasPredicate && _groupByMap.TryGetValue(projection, out info))
            {
                // use the element expression from the group-by info to rebind the argument so the resulting expression is one that 
                // would be legal to add to the columns in the select expression that has the corresponding group-by clause.
                if (argument != null)
                {
                    _map[argument.Parameters[0]] = info.Element;
                    argExpr = Visit(argument.Body);
                }
                else if (!hasPredicateArg || useAlternateArg)
                {
                    argExpr = info.Element;
                }
                aggExpr = new AggregateExpression(returnType, aggName, argExpr, isDistinct);

                // check for easy to optimize case.  If the projection that our aggregate is based on is really the 'group' argument from
                // the query.GroupBy(xxx, (key, group) => yyy) method then whatever expression we return here will automatically
                // become part of the select expression that has the group-by clause, so just return the simple aggregate expression.
                if (projection == _currentGroupElement)
                    return aggExpr;

                return new AggregateSubqueryExpression(info.Alias, aggExpr, subquery);
            }

            return subquery;
        }

        private Expression BindDistinct(Expression source)
        {
            var projection = VisitSequence(source);
            var select = projection.Select;
            var alias = GetNextAlias();

            var pc = ColumnProjector.ProjectColumns(_language, ProjectionAffinity.Server, projection.Projector, null, alias, projection.Select.Alias);
            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, projection.Select, null, null, null, true, null, null, false),
                pc.Projector
                );
        }

        private Expression BindTake(Expression source, Expression take)
        {
            var projection = VisitSequence(source);
            take = Visit(take);
            var select = projection.Select;
            var alias = GetNextAlias();
            var pc = ProjectColumns(projection.Projector, alias, projection.Select.Alias);
            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, projection.Select, null, null, null, false, null, take, false),
                pc.Projector
                );
        }

        private Expression BindSkip(Expression source, Expression skip)
        {
            var projection = VisitSequence(source);
            skip = Visit(skip);
            var select = projection.Select;
            var alias = GetNextAlias();
            var pc = ProjectColumns(projection.Projector, alias, projection.Select.Alias);
            return new ProjectionExpression(
                new SelectExpression(alias, pc.Columns, projection.Select, null, null, null, false, skip, null, false),
                pc.Projector
                );
        }

        private Expression BindCast(Expression source, Type targetElementType)
        {
            var projection = VisitSequence(source);
            var elementType = GetTrueUnderlyingType(projection.Projector);
            if (!targetElementType.IsAssignableFrom(elementType))
            {
                throw new InvalidOperationException(string.Format("Cannot cast elements from type '{0}' to type '{1}'", elementType, targetElementType));
            }
            return projection;
        }

        private Type GetTrueUnderlyingType(Expression expression)
        {
            while (expression.NodeType == ExpressionType.Convert)
            {
                expression = ((UnaryExpression)expression).Operand;
            }
            return expression.Type;
        }

        private Expression BindFirst(Expression source, LambdaExpression predicate, string kind, bool isRoot)
        {
            var projection = VisitSequence(source);
            Expression where = null;
            if (predicate != null)
            {
                _map[predicate.Parameters[0]] = projection.Projector;
                where = Visit(predicate.Body);
            }
            var isFirst = kind.StartsWith("First");
            var isLast = kind.StartsWith("Last");
            Expression take = (isFirst || isLast) ? Expression.Constant(1) : null;
            if (take != null || where != null)
            {
                var alias = GetNextAlias();
                var pc = ProjectColumns(projection.Projector, alias, projection.Select.Alias);
                projection = new ProjectionExpression(
                    new SelectExpression(alias, pc.Columns, projection.Select, where, null, null, false, null, take, isLast),
                    pc.Projector
                    );
            }
            if (isRoot)
            {
                var elementType = projection.Projector.Type;
                var p = Expression.Parameter(typeof(IEnumerable<>).MakeGenericType(elementType), "p");
                var gator = Expression.Lambda(Expression.Call(typeof(Enumerable), kind, new[] { elementType }, p), p);
                return new ProjectionExpression(projection.Select, projection.Projector, gator);
            }
            return projection;
        }

        private Expression BindAnyAll(Expression source, MethodInfo method, LambdaExpression predicate, bool isRoot)
        {
            var isAll = method.Name == "All";
            var constSource = source as ConstantExpression;
            if (constSource != null && !IsQuery(constSource))
            {
                Debug.Assert(!isRoot);
                Expression where = null;
                foreach (var value in (IEnumerable)constSource.Value)
                {
                    Expression expr = Expression.Invoke(predicate, Expression.Constant(value, predicate.Parameters[0].Type));
                    if (where == null)
                    {
                        where = expr;
                    }
                    else if (isAll)
                    {
                        where = where.And(expr);
                    }
                    else
                    {
                        where = where.Or(expr);
                    }
                }
                return Visit(where);
            }

            if (isAll)
            {
                predicate = Expression.Lambda(Expression.Not(predicate.Body), predicate.Parameters.ToArray());
            }
            if (predicate != null)
            {
                source = Expression.Call(typeof(Enumerable), "Where", method.GetGenericArguments(), source, predicate);
            }
            var projection = VisitSequence(source);
            Expression result = new ExistsExpression(projection.Select);
            if (isAll)
            {
                result = Expression.Not(result);
            }
            if (isRoot)
            {
                if (_language.AllowSubqueryInSelectWithoutFrom)
                {
                    return GetSingletonSequence(result, "SingleOrDefault");
                }

                // use count aggregate instead of exists
                var colType = _language.TypeSystem.GetColumnType(typeof(int));
                var newSelect = projection.Select.SetColumns(
                    new[] { new ColumnDeclaration("value", new AggregateExpression(typeof(int), "Count", null, false), colType) }
                );
                var colx = new ColumnExpression(typeof(int), colType, newSelect.Alias, "value");
                var exp = isAll 
                    ? colx.Equal(Expression.Constant(0))
                    : colx.GreaterThan(Expression.Constant(0));
                return new ProjectionExpression(
                    newSelect, exp, Aggregator.GetAggregator(typeof(bool), typeof(IEnumerable<bool>))
                );
            }
            return result;
        }

        private Expression BindContains(Expression source, Expression match, bool isRoot)
        {
            var constSource = source as ConstantExpression;
            if (constSource != null && !IsQuery(constSource))
            {
                Debug.Assert(!isRoot);
                var values = new List<Expression>();
                foreach (var value in (IEnumerable)constSource.Value)
                {
                    values.Add(Expression.Constant(Convert.ChangeType(value, match.Type), match.Type));
                }
                match = Visit(match);
                return new InExpression(match, values);
            }

            if (isRoot && !_language.AllowSubqueryInSelectWithoutFrom)
            {
                var p = Expression.Parameter(TypeHelper.GetElementType(source.Type), "x");
                var predicate = Expression.Lambda(p.Equal(match), p);
                var exp = Expression.Call(typeof(Queryable), "Any", new[] { p.Type }, source, predicate);
                _root = exp;
                return Visit(exp);
            }

            var projection = VisitSequence(source);
            match = Visit(match);
            Expression result = new InExpression(match, projection.Select);
            if (isRoot)
            {
                return GetSingletonSequence(result, "SingleOrDefault");
            }
            return result;
        }

        private Expression GetSingletonSequence(Expression expr, string aggregator)
        {
            var p = Expression.Parameter(typeof(IEnumerable<>).MakeGenericType(expr.Type), "p");
            LambdaExpression gator = null;
            if (aggregator != null)
            {
                gator = Expression.Lambda(Expression.Call(typeof(Enumerable), aggregator, new[] { expr.Type }, p), p);
            }
            var alias = GetNextAlias();
            var colType = _language.TypeSystem.GetColumnType(expr.Type);
            var select = new SelectExpression(alias, new[] { new ColumnDeclaration("value", expr, colType) }, null, null);
            return new ProjectionExpression(select, new ColumnExpression(expr.Type, colType, alias, "value"), gator);
        }

        private Expression BindInsert(IEntityTable upd, Expression instance, LambdaExpression selector)
        {
            var entity = _mapper.Mapping.GetEntity(instance.Type, upd.TableId);
            return Visit(_mapper.GetInsertExpression(entity, instance, selector));
        }

        private Expression BindUpdate(IEntityTable upd, Expression instance, LambdaExpression updateCheck, LambdaExpression resultSelector)
        {
            var entity = _mapper.Mapping.GetEntity(instance.Type, upd.TableId);
            return Visit(_mapper.GetUpdateExpression(entity, instance, updateCheck, resultSelector, null));
        }

        private Expression BindInsertOrUpdate(IEntityTable upd, Expression instance, LambdaExpression updateCheck, LambdaExpression resultSelector)
        {
            var entity = _mapper.Mapping.GetEntity(instance.Type, upd.TableId);
            return Visit(_mapper.GetInsertOrUpdateExpression(entity, instance, updateCheck, resultSelector));
        }

        private Expression BindDelete(IEntityTable upd, Expression instance, LambdaExpression deleteCheck)
        {
            var entity = _mapper.Mapping.GetEntity(instance != null ? instance.Type : deleteCheck.Parameters[0].Type, upd.TableId);
            return Visit(_mapper.GetDeleteExpression(entity, instance, deleteCheck));
        }

        private Expression BindBatch(IEntityTable upd, Expression instances, LambdaExpression operation, Expression batchSize, Expression stream)
        {
            var save = _batchUpd;
            _batchUpd = upd;
            var op = (LambdaExpression)Visit(operation);
            _batchUpd = save;
            var items = Visit(instances);
            var size = Visit(batchSize);
            var str = Visit(stream);
            return new BatchExpression(items, op, size, str);
        }

        private bool IsQuery(Expression expression)
        {
            var elementType = TypeHelper.GetElementType(expression.Type);
            return elementType != null && typeof(IQueryable<>).MakeGenericType(elementType).IsAssignableFrom(expression.Type);
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            if (IsQuery(c))
            {
                var q = (IQueryable)c.Value;
                var t = q as IEntityTable;
                if (t != null)
                {
                    var ihme = t as IHaveMappingEntity;
                    var entity = ihme != null ? ihme.Entity : _mapper.Mapping.GetEntity(t.ElementType, t.TableId);
                    return VisitSequence(_mapper.GetQueryExpression(entity));
                }

                if (q.Expression.NodeType == ExpressionType.Constant)
                {
                    // assume this is also a table via some other implementation of IQueryable
                    var entity = _mapper.Mapping.GetEntity(q.ElementType);
                    return VisitSequence(_mapper.GetQueryExpression(entity));
                }

                var pev = PartialEvaluator.Eval(q.Expression, _mapper.Mapping.CanBeEvaluatedLocally);
                return Visit(pev);
            }
            return c;
        }

        protected override Expression VisitParameter(ParameterExpression p)
        {
            Expression e;
            if (_map.TryGetValue(p, out e))
            {
                return e;
            }
            return p;
        }

        protected override Expression VisitInvocation(InvocationExpression iv)
        {
            var lambda = iv.Expression as LambdaExpression;
            if (lambda != null)
            {
                for (int i = 0, n = lambda.Parameters.Count; i < n; i++)
                {
                    _map[lambda.Parameters[i]] = iv.Arguments[i];
                }
                return Visit(lambda.Body);
            }
            return base.VisitInvocation(iv);
        }

        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            if (m.Expression != null 
                && m.Expression.NodeType == ExpressionType.Parameter 
                && !_map.ContainsKey((ParameterExpression)m.Expression)
                && IsQuery(m))
            {
                return VisitSequence(_mapper.GetQueryExpression(_mapper.Mapping.GetEntity(m.Member)));
            }
            var source = Visit(m.Expression);
            if (_language.IsAggregate(m.Member) && IsRemoteQuery(source))
            {
                return BindAggregate(m.Expression, m.Member.Name, TypeHelper.GetMemberType(m.Member), null, m == _root);
            }
            var result = BindMember(source, m.Member);
            var mex = result as MemberExpression;
            if (mex != null && mex.Member == m.Member && mex.Expression == m.Expression)
            {
                return m;
            }
            return result;
        }

        private bool IsRemoteQuery(Expression expression)
        {
            if (expression.NodeType.IsDbExpression())
                return true;
            switch (expression.NodeType)
            {
                case ExpressionType.MemberAccess:
                    return IsRemoteQuery(((MemberExpression)expression).Expression);
                case ExpressionType.Call:
                    var mc = (MethodCallExpression)expression;
                    if (mc.Object != null)
                        return IsRemoteQuery(mc.Object);
                    else if (mc.Arguments.Count > 0)
                        return IsRemoteQuery(mc.Arguments[0]);
                    break;
            }
            return false;
        }

        public static Expression BindMember(Expression source, MemberInfo member)
        {
            switch (source.NodeType)
            {
                case (ExpressionType)DbExpressionType.Entity:
                    var ex = (EntityExpression)source;
                    var result = BindMember(ex.Expression, member);
                    var mex = result as MemberExpression;
                    if (mex != null && mex.Expression == ex.Expression && mex.Member == member)
                    {
                        return Expression.MakeMemberAccess(source, member);
                    }
                    return result;

                case ExpressionType.Convert:
                    var ux = (UnaryExpression)source;
                    return BindMember(ux.Operand, member);

                case ExpressionType.MemberInit:
                    var min = (MemberInitExpression)source;
                    for (int i = 0, n = min.Bindings.Count; i < n; i++)
                    {
                        var assign = min.Bindings[i] as MemberAssignment;
                        if (assign != null && MembersMatch(assign.Member, member))
                        {                            
                            return assign.Expression;
                        }
                    }
                    break;

                case ExpressionType.New:
                    var nex = (NewExpression)source;
                    if (nex.Members != null)
                    {
                        for (int i = 0, n = nex.Members.Count; i < n; i++)
                        {
                            if (MembersMatch(nex.Members[i], member))
                            {
                                return nex.Arguments[i];
                            }
                        }
                    }
                    else if (nex.Type.IsGenericType && nex.Type.GetGenericTypeDefinition() == typeof(Grouping<,>))
                    {
                        if (member.Name == "Key")
                        {
                            return nex.Arguments[0];
                        }
                    }
                    break;

                case (ExpressionType)DbExpressionType.Projection:
                    // member access on a projection turns into a new projection w/ member access applied
                    var proj = (ProjectionExpression)source;
                    var newProjector = BindMember(proj.Projector, member);
                    var mt = TypeHelper.GetMemberType(member);
                    return new ProjectionExpression(proj.Select, newProjector, Aggregator.GetAggregator(mt, typeof(IEnumerable<>).MakeGenericType(mt)));

                case (ExpressionType)DbExpressionType.OuterJoined:
                    var oj = (OuterJoinedExpression)source;
                    var em = BindMember(oj.Expression, member);
                    if (em is ColumnExpression)
                    {
                        return em;
                    }
                    return new OuterJoinedExpression(oj.Test, em);

                case ExpressionType.Conditional:
                    var cex = (ConditionalExpression)source;
                    return Expression.Condition(cex.Test, BindMember(cex.IfTrue, member), BindMember(cex.IfFalse, member));

                case ExpressionType.Constant:
                    var con = (ConstantExpression)source;
                    var memberType = TypeHelper.GetMemberType(member);
                    if (con.Value == null)
                    {
                        return Expression.Constant(GetDefault(memberType), memberType);
                    }
                    else
                    {
                        return Expression.Constant(GetValue(con.Value, member), memberType);
                    }
            }
            return Expression.MakeMemberAccess(source, member);
        }

        private static object GetValue(object instance, MemberInfo member)
        {
            var fi = member as FieldInfo;
            if (fi != null)
            {
                return fi.GetValue(instance);
            }
            var pi = member as PropertyInfo;
            if (pi != null)
            {
                return pi.GetValue(instance, null);
            }
            return null;
        }

        private static object GetDefault(Type type)
        {
            if (!type.IsValueType || TypeHelper.IsNullableType(type))
            {
                return null;
            }

            return Activator.CreateInstance(type);
        }

        private static bool MembersMatch(MemberInfo a, MemberInfo b)
        {
            if (a.Name == b.Name)
            {
                return true;
            }
            if (a is MethodInfo && b is PropertyInfo)
            {
                return a.Name == ((PropertyInfo)b).GetGetMethod().Name;
            }

            if (a is PropertyInfo && b is MethodInfo)
            {
                return ((PropertyInfo)a).GetGetMethod().Name == b.Name;
            }
            return false;
        }
    }
}
