﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Translation;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Language
{
    /// <summary>
    /// Defines the language rules for the query provider
    /// </summary>
    public abstract class QueryLanguage
    {
        public abstract QueryTypeSystem TypeSystem { get; }
        public abstract Expression GetGeneratedIdExpression(MemberInfo member);

        public virtual string Quote(string name)
        {
            return name;
        }

        public virtual bool AllowsMultipleCommands => false;

        public virtual bool AllowSubqueryInSelectWithoutFrom => false;

        public virtual bool AllowDistinctInAggregates => false;

        public virtual Expression GetRowsAffectedExpression(Expression command)
        {
            return new FunctionExpression(typeof(int), "@@ROWCOUNT", null);
        }

        public virtual bool IsRowsAffectedExpressions(Expression expression)
        {
            var fex = expression as FunctionExpression;
            return fex != null && fex.Name == "@@ROWCOUNT";
        }

        public virtual Expression GetOuterJoinTest(SelectExpression select)
        {
            // if the column is used in the join condition (equality test)
            // if it is null in the database then the join test won't match (null != null) so the row won't appear
            // we can safely use this existing column as our test to determine if the outer join produced a row

            // find a column that is used in equality test
            var aliases = DeclaredAliasGatherer.Gather(select.From);
            var joinColumns = JoinColumnGatherer.Gather(aliases, select).ToList();
            if (joinColumns.Count > 0)
            {
                // prefer one that is already in the projection list.
                foreach (var jc in joinColumns)
                {
                    foreach (var col in select.Columns)
                    {
                        if (jc.Equals(col.Expression))
                        {
                            return jc;
                        }
                    }
                }
                return joinColumns[0];
            }

            // fall back to introducing a constant
            return Expression.Constant(1, typeof(int?));
        }

        public virtual ProjectionExpression AddOuterJoinTest(ProjectionExpression proj)
        {
            var test = GetOuterJoinTest(proj.Select);
            var select = proj.Select;
            ColumnExpression testCol = null;
            // look to see if test expression exists in columns already
            foreach (var col in select.Columns)
            {
                if (test.Equals(col.Expression))
                {
                    var colType = TypeSystem.GetColumnType(test.Type);
                    testCol = new ColumnExpression(test.Type, colType, select.Alias, col.Name);
                    break;
                }
            }
            if (testCol == null)
            {
                // add expression to projection
                testCol = test as ColumnExpression;
                var colName = (testCol != null) ? testCol.Name : "Test";
                colName = proj.Select.Columns.GetAvailableColumnName(colName);
                var colType = TypeSystem.GetColumnType(test.Type);
                select = select.AddColumn(new ColumnDeclaration(colName, test, colType));
                testCol = new ColumnExpression(test.Type, colType, select.Alias, colName);
            }
            var newProjector = new OuterJoinedExpression(testCol, proj.Projector);
            return new ProjectionExpression(select, newProjector, proj.Aggregator);
        }

        private class JoinColumnGatherer
        {
            private readonly HashSet<TableAlias> _aliases;
            private readonly HashSet<ColumnExpression> _columns = new HashSet<ColumnExpression>();

            private JoinColumnGatherer(HashSet<TableAlias> aliases)
            {
                _aliases = aliases;
            }

            public static HashSet<ColumnExpression> Gather(HashSet<TableAlias> aliases, SelectExpression select)
            {
                var gatherer = new JoinColumnGatherer(aliases);
                gatherer.Gather(select.Where);
                return gatherer._columns;
            }

            private void Gather(Expression expression)
            {
                var b = expression as BinaryExpression;
                if (b != null)
                {
                    switch (b.NodeType)
                    {
                        case ExpressionType.Equal:
                        case ExpressionType.NotEqual:
                            if (IsExternalColumn(b.Left) && GetColumn(b.Right) != null)
                            {
                                _columns.Add(GetColumn(b.Right));
                            }
                            else if (IsExternalColumn(b.Right) && GetColumn(b.Left) != null)
                            {
                                _columns.Add(GetColumn(b.Left));
                            }
                            break;
                        case ExpressionType.And:
                        case ExpressionType.AndAlso:
                            if (b.Type == typeof(bool) || b.Type == typeof(bool?))
                            {
                                Gather(b.Left);
                                Gather(b.Right);
                            }
                            break;
                    }
                }
            }

            private ColumnExpression GetColumn(Expression exp)
            {
                while (exp.NodeType == ExpressionType.Convert)
                    exp = ((UnaryExpression)exp).Operand;
                return exp as ColumnExpression;
            }

            private bool IsExternalColumn(Expression exp)
            {
                var col = GetColumn(exp);
                if (col != null && !_aliases.Contains(col.Alias))
                    return true;
                return false;
            }
        }

        /// <summary>
        /// Determines whether the CLR type corresponds to a scalar data type in the query language
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public virtual bool IsScalar(Type type)
        {
            type = TypeHelper.GetNonNullableType(type);
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Empty:
                case TypeCode.DBNull:
                    return false;
                case TypeCode.Object:
                    return
                        type == typeof(DateTimeOffset) ||
                        type == typeof(TimeSpan) ||
                        type == typeof(Guid) ||
                        type == typeof(byte[]);
                default:
                    return true;
            }
        }

        public virtual bool IsAggregate(MemberInfo member)
        {
            var method = member as MethodInfo;
            if (method != null)
            {
                if (method.DeclaringType == typeof(Queryable)
                    || method.DeclaringType == typeof(Enumerable))
                {
                    switch (method.Name)
                    {
                        case "Count":
                        case "LongCount":
                        case "Sum":
                        case "Min":
                        case "Max":
                        case "Average":
                            return true;
                    }
                }
            }
            var property = member as PropertyInfo;
            if (property != null
                && property.Name == "Count"
                && typeof(IEnumerable).IsAssignableFrom(property.DeclaringType))
            {
                return true;
            }
            return false;
        }

        public virtual bool AggregateArgumentIsPredicate(string aggregateName)
        {
            return aggregateName == "Count" || aggregateName == "LongCount";
        }

        /// <summary>
        /// Determines whether the given expression can be represented as a column in a select expressionss
        /// </summary>
        public virtual bool CanBeColumn(Expression expression)
        {
            return MustBeColumn(expression) || IsScalar(expression.Type);
        }

        /// <summary>
        /// Determines whether the given expression must be represented as a column in a SELECT column list
        /// </summary>
        public virtual bool MustBeColumn(Expression expression)
        {
            switch (expression.NodeType)
            {
                case (ExpressionType)DbExpressionType.Column:
                case (ExpressionType)DbExpressionType.Scalar:
                case (ExpressionType)DbExpressionType.Exists:
                case (ExpressionType)DbExpressionType.AggregateSubquery:
                case (ExpressionType)DbExpressionType.Aggregate:
                    return true;
                default:
                    return false;
            }
        }

        public virtual QueryLinguist CreateLinguist(QueryTranslator translator)
        {
            return new QueryLinguist(this, translator);
        }
    }
}