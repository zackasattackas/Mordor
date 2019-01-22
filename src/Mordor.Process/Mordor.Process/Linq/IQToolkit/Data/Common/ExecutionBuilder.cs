// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;
using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;
using Mordor.Process.Linq.IQToolkit.Data.Common.Translation;

namespace Mordor.Process.Linq.IQToolkit.Data.Common
{
    /// <summary>
    /// Builds an execution plan for a query expression
    /// </summary>
    public class ExecutionBuilder : DbExpressionVisitor
    {
        private readonly QueryPolicy _policy;
        private readonly QueryLinguist _linguist;
        private readonly Expression _executor;
        private Scope _scope;
        private bool _isTop = true;
        private MemberInfo _receivingMember;
        private int _nReaders;
        private readonly List<ParameterExpression> _variables = new List<ParameterExpression>();
        private readonly List<Expression> _initializers = new List<Expression>();
        private Dictionary<string, Expression> _variableMap = new Dictionary<string, Expression>();

        private ExecutionBuilder(QueryLinguist linguist, QueryPolicy policy, Expression executor)
        {
            _linguist = linguist;
            _policy = policy;
            _executor = executor;
        }

        public static Expression Build(QueryLinguist linguist, QueryPolicy policy, Expression expression, Expression provider)
        {
            var executor = Expression.Parameter(typeof(QueryExecutor), "executor");
            var builder = new ExecutionBuilder(linguist, policy, executor);
            builder._variables.Add(executor);
            builder._initializers.Add(Expression.Call(Expression.Convert(provider, typeof(ICreateExecutor)), "CreateExecutor", null, null));
            var result = builder.Build(expression);
            return result;
        }

        private Expression Build(Expression expression)
        {
            expression = Visit(expression);
            expression = AddVariables(expression);
            return expression;
        }

        private Expression AddVariables(Expression expression)
        {
            // add variable assignments up front
            if (_variables.Count > 0)
            {
                var exprs = new List<Expression>();
                for (int i = 0, n = _variables.Count; i < n; i++)
                {
                    exprs.Add(MakeAssign(_variables[i], _initializers[i]));
                }
                exprs.Add(expression);
                var sequence = MakeSequence(exprs);  // yields last expression value

                // use invoke/lambda to create variables via parameters in scope
                Expression[] nulls = _variables.Select(v => Expression.Constant(null, v.Type)).ToArray();
                expression = Expression.Invoke(Expression.Lambda(sequence, _variables.ToArray()), nulls);
            }

            return expression;
        }

        private static Expression MakeSequence(IList<Expression> expressions)
        {
            var last = expressions[expressions.Count - 1];
            expressions = expressions.Select(e => e.Type.IsValueType ? Expression.Convert(e, typeof(object)) : e).ToList();
            return Expression.Convert(Expression.Call(typeof(ExecutionBuilder), "Sequence", null, Expression.NewArrayInit(typeof(object), expressions)), last.Type);
        }

        public static object Sequence(params object[] values) 
        {
            return values[values.Length - 1];
        }

        public static IEnumerable<TR> Batch<T, TR>(IEnumerable<T> items, Func<T,TR> selector, bool stream)
        {
            var result = items.Select(selector);
            if (!stream)
            {
                return result.ToList();
            }

            return new EnumerateOnce<TR>(result);
        }

        private static Expression MakeAssign(ParameterExpression variable, Expression value)
        {
            return Expression.Call(typeof(ExecutionBuilder), "Assign", new[] { variable.Type }, variable, value);
        }

        public static T Assign<T>(ref T variable, T value)
        {
            variable = value;
            return value;
        }

        private Expression BuildInner(Expression expression)
        {
            var eb = new ExecutionBuilder(_linguist, _policy, _executor);
            eb._scope = _scope;
            eb._receivingMember = _receivingMember;
            eb._nReaders = _nReaders;
            eb._nLookup = _nLookup;
            eb._variableMap = _variableMap;
            return eb.Build(expression);
        }

        protected override MemberBinding VisitBinding(MemberBinding binding)
        {
            var save = _receivingMember;
            _receivingMember = binding.Member;
            var result = base.VisitBinding(binding);
            _receivingMember = save;
            return result;
        }

        private int _nLookup;

        private Expression MakeJoinKey(IList<Expression> key)
        {
            if (key.Count == 1)
            {
                return key[0];
            }

            return Expression.New(
                typeof(CompoundKey).GetConstructors()[0],
                Expression.NewArrayInit(typeof(object), key.Select(k => (Expression)Expression.Convert(k, typeof(object))))
            );
        }

        protected override Expression VisitClientJoin(ClientJoinExpression join)
        {
            // convert client join into a up-front lookup table builder & replace client-join in tree with lookup accessor

            // 1) lookup = query.Select(e => new KVP(key: inner, value: e)).ToLookup(kvp => kvp.Key, kvp => kvp.Value)
            var innerKey = MakeJoinKey(join.InnerKey);
            var outerKey = MakeJoinKey(join.OuterKey);

            var kvpConstructor = typeof(KeyValuePair<,>).MakeGenericType(innerKey.Type, join.Projection.Projector.Type).GetConstructor(new[] { innerKey.Type, join.Projection.Projector.Type });
            Expression constructKvPair = Expression.New(kvpConstructor, innerKey, join.Projection.Projector);
            var newProjection = new ProjectionExpression(join.Projection.Select, constructKvPair);

            var iLookup = ++_nLookup;
            var execution = ExecuteProjection(newProjection, false);

            var kvp = Expression.Parameter(constructKvPair.Type, "kvp");

            // filter out nulls
            if (join.Projection.Projector.NodeType == (ExpressionType)DbExpressionType.OuterJoined)
            {
                var pred = Expression.Lambda(
                    Expression.PropertyOrField(kvp, "Value").NotEqual(TypeHelper.GetNullConstant(join.Projection.Projector.Type)),
                    kvp
                    );
                execution = Expression.Call(typeof(Enumerable), "Where", new[] { kvp.Type }, execution, pred);
            }

            // make lookup
            var keySelector = Expression.Lambda(Expression.PropertyOrField(kvp, "Key"), kvp);
            var elementSelector = Expression.Lambda(Expression.PropertyOrField(kvp, "Value"), kvp);
            Expression toLookup = Expression.Call(typeof(Enumerable), "ToLookup", new[] { kvp.Type, outerKey.Type, join.Projection.Projector.Type }, execution, keySelector, elementSelector);

            // 2) agg(lookup[outer])
            var lookup = Expression.Parameter(toLookup.Type, "lookup" + iLookup);
            var property = lookup.Type.GetProperty("Item");
            Expression access = Expression.Call(lookup, property.GetGetMethod(), Visit(outerKey));
            if (join.Projection.Aggregator != null)
            {
                // apply aggregator
                access = DbExpressionReplacer.Replace(join.Projection.Aggregator.Body, join.Projection.Aggregator.Parameters[0], access);
            }

            _variables.Add(lookup);
            _initializers.Add(toLookup);

            return access;
        }

        protected override Expression VisitProjection(ProjectionExpression projection)
        {
            if (_isTop)
            {
                _isTop = false;
                return ExecuteProjection(projection, _scope != null);
            }

            return BuildInner(projection);
        }

        protected virtual Expression Parameterize(Expression expression)
        {
            if (_variableMap.Count > 0)
            {
                expression = VariableSubstitutor.Substitute(_variableMap, expression);
            }
            return _linguist.Parameterize(expression);
        }

        private Expression ExecuteProjection(ProjectionExpression projection, bool okayToDefer)
        {
            // parameterize query
            projection = (ProjectionExpression)Parameterize(projection);

            if (_scope != null)
            {
                // also convert references to outer alias to named values!  these become SQL parameters too
                projection = (ProjectionExpression)OuterParameterizer.Parameterize(_scope.Alias, projection);
            }

            var commandText = _linguist.Format(projection.Select);
            var namedValues = NamedValueGatherer.Gather(projection.Select);
            var command = new QueryCommand(commandText, namedValues.Select(v => new QueryParameter(v.Name, v.Type, v.QueryType)));
            Expression[] values = namedValues.Select(v => Expression.Convert(Visit(v.Value), typeof(object))).ToArray();

            return ExecuteProjection(projection, okayToDefer, command, values);
        }

        private Expression ExecuteProjection(ProjectionExpression projection, bool okayToDefer, QueryCommand command, Expression[] values)
        {
            okayToDefer &= (_receivingMember != null && _policy.IsDeferLoaded(_receivingMember));

            var saveScope = _scope;
            var reader = Expression.Parameter(typeof(FieldReader), "r" + _nReaders++);
            _scope = new Scope(_scope, reader, projection.Select.Alias, projection.Select.Columns);
            var projector = Expression.Lambda(Visit(projection.Projector), reader);
            _scope = saveScope;

            var entity = EntityFinder.Find(projection.Projector);

            var methExecute = okayToDefer 
                ? "ExecuteDeferred" 
                : "Execute";

            // call low-level execute directly on supplied DbQueryProvider
            Expression result = Expression.Call(_executor, methExecute, new[] { projector.Body.Type },
                Expression.Constant(command),
                projector,
                Expression.Constant(entity, typeof(MappingEntity)),
                Expression.NewArrayInit(typeof(object), values)
                );

            if (projection.Aggregator != null)
            {
                // apply aggregator
                result = DbExpressionReplacer.Replace(projection.Aggregator.Body, projection.Aggregator.Parameters[0], result);
            }
            return result;
        }

        protected override Expression VisitBatch(BatchExpression batch)
        {
            if (_linguist.Language.AllowsMultipleCommands || !IsMultipleCommands(batch.Operation.Body as CommandExpression))
            {
                return BuildExecuteBatch(batch);
            }

            var source = Visit(batch.Input);
            var op = Visit(batch.Operation.Body);
            var fn = Expression.Lambda(op, batch.Operation.Parameters[1]);
            return Expression.Call(GetType(), "Batch", new[] {TypeHelper.GetElementType(source.Type), batch.Operation.Body.Type}, source, fn, batch.Stream);
        }

        protected virtual Expression BuildExecuteBatch(BatchExpression batch)
        {
            // parameterize query
            var operation = Parameterize(batch.Operation.Body);

            var commandText = _linguist.Format(operation);
            var namedValues = NamedValueGatherer.Gather(operation);
            var command = new QueryCommand(commandText, namedValues.Select(v => new QueryParameter(v.Name, v.Type, v.QueryType)));
            Expression[] values = namedValues.Select(v => Expression.Convert(Visit(v.Value), typeof(object))).ToArray();

            Expression paramSets = Expression.Call(typeof(Enumerable), "Select", new[] { batch.Operation.Parameters[1].Type, typeof(object[]) },
                batch.Input,
                Expression.Lambda(Expression.NewArrayInit(typeof(object), values), batch.Operation.Parameters[1])
                );

            Expression plan = null;

            var projection = ProjectionFinder.FindProjection(operation);
            if (projection != null)
            {
                var saveScope = _scope;
                var reader = Expression.Parameter(typeof(FieldReader), "r" + _nReaders++);
                _scope = new Scope(_scope, reader, projection.Select.Alias, projection.Select.Columns);
                var projector = Expression.Lambda(Visit(projection.Projector), reader);
                _scope = saveScope;

                var entity = EntityFinder.Find(projection.Projector);
                command = new QueryCommand(command.CommandText, command.Parameters);

                plan = Expression.Call(_executor, "ExecuteBatch", new[] { projector.Body.Type },
                    Expression.Constant(command),
                    paramSets,
                    projector,
                    Expression.Constant(entity, typeof(MappingEntity)),
                    batch.BatchSize,
                    batch.Stream
                    );
            }
            else
            {
                plan = Expression.Call(_executor, "ExecuteBatch", null,
                    Expression.Constant(command),
                    paramSets,
                    batch.BatchSize,
                    batch.Stream
                    );
            }

            return plan;
        }

        protected override Expression VisitCommand(CommandExpression command)
        {
            if (_linguist.Language.AllowsMultipleCommands || !IsMultipleCommands(command))
            {
                return BuildExecuteCommand(command);
            }

            return base.VisitCommand(command);
        }

        protected virtual bool IsMultipleCommands(CommandExpression command)
        {
            if (command == null)
                return false;
            switch ((DbExpressionType)command.NodeType)
            {
                case DbExpressionType.Insert:
                case DbExpressionType.Delete:
                case DbExpressionType.Update:
                    return false;
                default:
                    return true;
            }
        }

        protected override Expression VisitInsert(InsertCommand insert)
        {
            return BuildExecuteCommand(insert);
        }

        protected override Expression VisitUpdate(UpdateCommand update)
        {
            return BuildExecuteCommand(update);
        }

        protected override Expression VisitDelete(DeleteCommand delete)
        {
            return BuildExecuteCommand(delete);
        }

        protected override Expression VisitBlock(BlockCommand block)
        {
            return MakeSequence(VisitExpressionList(block.Commands));
        }

        protected override Expression VisitIf(IfCommand ifx)
        {
            var test = 
                Expression.Condition(
                    ifx.Check, 
                    ifx.IfTrue, 
                    ifx.IfFalse != null 
                        ? ifx.IfFalse 
                        : ifx.IfTrue.Type == typeof(int) 
                            ? Expression.Property(_executor, "RowsAffected") 
                            : (Expression)Expression.Constant(TypeHelper.GetDefault(ifx.IfTrue.Type), ifx.IfTrue.Type)
                            );
            return Visit(test);
        }

        protected override Expression VisitFunction(FunctionExpression func)
        {
            if (_linguist.Language.IsRowsAffectedExpressions(func))
            {
                return Expression.Property(_executor, "RowsAffected");
            }
            return base.VisitFunction(func);
        }

        protected override Expression VisitExists(ExistsExpression exists)
        {
            // how did we get here? Translate exists into count query
            var colType = _linguist.Language.TypeSystem.GetColumnType(typeof(int));
            var newSelect = exists.Select.SetColumns(
                new[] { new ColumnDeclaration("value", new AggregateExpression(typeof(int), "Count", null, false), colType) }
                );

            var projection = 
                new ProjectionExpression(
                    newSelect,
                    new ColumnExpression(typeof(int), colType, newSelect.Alias, "value"),
                    Aggregator.GetAggregator(typeof(int), typeof(IEnumerable<int>))
                    );

            var expression = projection.GreaterThan(Expression.Constant(0));

            return Visit(expression);
        }

        protected override Expression VisitDeclaration(DeclarationCommand decl)
        {
            if (decl.Source != null)
            {
                // make query that returns all these declared values as an object[]
                var projection = new ProjectionExpression(
                    decl.Source,
                    Expression.NewArrayInit(
                        typeof(object),
                        decl.Variables.Select(v => v.Expression.Type.IsValueType
                            ? Expression.Convert(v.Expression, typeof(object))
                            : v.Expression).ToArray()
                        ),
                    Aggregator.GetAggregator(typeof(object[]), typeof(IEnumerable<object[]>))
                    );

                // create execution variable to hold the array of declared variables
                var vars = Expression.Parameter(typeof(object[]), "vars");
                _variables.Add(vars);
                _initializers.Add(Expression.Constant(null, typeof(object[])));

                // create subsitution for each variable (so it will find the variable value in the new vars array)
                for (int i = 0, n = decl.Variables.Count; i < n; i++)
                {
                    var v = decl.Variables[i];
                    var nv = new NamedValueExpression(
                        v.Name, v.QueryType,
                        Expression.Convert(Expression.ArrayIndex(vars, Expression.Constant(i)), v.Expression.Type)
                        );
                    _variableMap.Add(v.Name, nv);
                }

                // make sure the execution of the select stuffs the results into the new vars array
                return MakeAssign(vars, Visit(projection));
            }

            // probably bad if we get here since we must not allow mulitple commands
            throw new InvalidOperationException("Declaration query not allowed for this langauge");
        }
        
        protected virtual Expression BuildExecuteCommand(CommandExpression command)
        {
            // parameterize query
            var expression = Parameterize(command);

            var commandText = _linguist.Format(expression);
            var namedValues = NamedValueGatherer.Gather(expression);
            var qc = new QueryCommand(commandText, namedValues.Select(v => new QueryParameter(v.Name, v.Type, v.QueryType)));
            Expression[] values = namedValues.Select(v => Expression.Convert(Visit(v.Value), typeof(object))).ToArray();

            var projection = ProjectionFinder.FindProjection(expression);
            if (projection != null)
            {
                return ExecuteProjection(projection, false, qc, values);
            }

            Expression plan = Expression.Call(_executor, "ExecuteCommand", null,
                Expression.Constant(qc),
                Expression.NewArrayInit(typeof(object), values)
                );

            return plan;
        }

        protected override Expression VisitEntity(EntityExpression entity)
        {
            return Visit(entity.Expression);
        }

        protected override Expression VisitOuterJoined(OuterJoinedExpression outer)
        {
            var expr = Visit(outer.Expression);
            var column = (ColumnExpression)outer.Test;
            ParameterExpression reader;
            int iOrdinal;
            if (_scope.TryGetValue(column, out reader, out iOrdinal))
            {
                return Expression.Condition(
                    Expression.Call(reader, "IsDbNull", null, Expression.Constant(iOrdinal)),
                    Expression.Constant(TypeHelper.GetDefault(outer.Type), outer.Type),
                    expr
                    );
            }
            return expr;
        }

        protected override Expression VisitColumn(ColumnExpression column)
        {
            ParameterExpression fieldReader;
            int iOrdinal;
            if (_scope != null && _scope.TryGetValue(column, out fieldReader, out iOrdinal))
            {
                var method = FieldReader.GetReaderMethod(column.Type);
                return Expression.Call(fieldReader, method, Expression.Constant(iOrdinal));
            }

            Debug.Fail(string.Format("column not in scope: {0}", column));
            return column;
        }

        private class Scope
        {
            private readonly Scope _outer;
            private readonly ParameterExpression _fieldReader;
            internal TableAlias Alias { get; private set; }
            private readonly Dictionary<string, int> _nameMap;

            internal Scope(Scope outer, ParameterExpression fieldReader, TableAlias alias, IEnumerable<ColumnDeclaration> columns)
            {
                _outer = outer;
                _fieldReader = fieldReader;
                Alias = alias;
                _nameMap = columns.Select((c, i) => new { c, i }).ToDictionary(x => x.c.Name, x => x.i);
            }

            internal bool TryGetValue(ColumnExpression column, out ParameterExpression fieldReader, out int ordinal)
            {
                for (var s = this; s != null; s = s._outer)
                {
                    if (column.Alias == s.Alias && _nameMap.TryGetValue(column.Name, out ordinal))
                    {
                        fieldReader = _fieldReader;
                        return true;
                    }
                }
                fieldReader = null;
                ordinal = 0;
                return false;
            }
        }

        /// <summary>
        /// columns referencing the outer alias are turned into special named-value parameters
        /// </summary>
        private class OuterParameterizer : DbExpressionVisitor
        {
            private int _iParam;
            private TableAlias _outerAlias;
            private readonly Dictionary<ColumnExpression, NamedValueExpression> _map = new Dictionary<ColumnExpression, NamedValueExpression>();

            internal static Expression Parameterize(TableAlias outerAlias, Expression expr)
            {
                var op = new OuterParameterizer();
                op._outerAlias = outerAlias;
                return op.Visit(expr);
            }

            protected override Expression VisitProjection(ProjectionExpression proj)
            {
                var select = (SelectExpression)Visit(proj.Select);
                return UpdateProjection(proj, select, proj.Projector, proj.Aggregator);
            }

            protected override Expression VisitColumn(ColumnExpression column)
            {
                if (column.Alias == _outerAlias)
                {
                    NamedValueExpression nv;
                    if (!_map.TryGetValue(column, out nv)) 
                    {
                        nv = new NamedValueExpression("n" + (_iParam++), column.QueryType, column);
                        _map.Add(column, nv);
                    }
                    return nv;
                }
                return column;
            }
        }

        private class ColumnGatherer : DbExpressionVisitor
        {
            private readonly Dictionary<string, ColumnExpression> _columns = new Dictionary<string, ColumnExpression>();

            internal static IEnumerable<ColumnExpression> Gather(Expression expression)
            {
                var gatherer = new ColumnGatherer();
                gatherer.Visit(expression);
                return gatherer._columns.Values;
            }

            protected override Expression VisitColumn(ColumnExpression column)
            {
                if (!_columns.ContainsKey(column.Name))
                {
                    _columns.Add(column.Name, column);
                }
                return column;
            }
        }

        private class ProjectionFinder : DbExpressionVisitor
        {
            private ProjectionExpression _found;

            internal static ProjectionExpression FindProjection(Expression expression)
            {
                var finder = new ProjectionFinder();
                finder.Visit(expression);
                return finder._found;
            }

            protected override Expression VisitProjection(ProjectionExpression proj)
            {
                _found = proj;
                return proj;
            }
        }

        private class VariableSubstitutor : DbExpressionVisitor
        {
            private readonly Dictionary<string, Expression> _map;

            private VariableSubstitutor(Dictionary<string, Expression> map)
            {
                _map = map;
            }

            public static Expression Substitute(Dictionary<string, Expression> map, Expression expression)
            {
                return new VariableSubstitutor(map).Visit(expression);
            }

            protected override Expression VisitVariable(VariableExpression vex)
            {
                Expression sub;
                if (_map.TryGetValue(vex.Name, out sub))
                {
                    return sub;
                }
                return vex;
            }
        }

        private class EntityFinder : DbExpressionVisitor
        {
            private MappingEntity _entity;

            public static MappingEntity Find(Expression expression)
            {
                var finder = new EntityFinder();
                finder.Visit(expression);
                return finder._entity;
            }

            protected override Expression Visit(Expression exp)
            {
                if (_entity == null)
                    return base.Visit(exp);
                return exp;
            }

            protected override Expression VisitEntity(EntityExpression entity)
            {
                if (_entity == null)
                    _entity = entity.Entity;
                return entity;
            }

            protected override NewExpression VisitNew(NewExpression nex)
            {
                return nex;
            }

            protected override Expression VisitMemberInit(MemberInitExpression init)
            {
                return init;
            }
        }
    }
}