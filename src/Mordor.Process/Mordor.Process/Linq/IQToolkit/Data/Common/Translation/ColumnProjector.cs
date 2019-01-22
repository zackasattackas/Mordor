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
    /// Splits an expression into two parts
    ///   1) a list of column declarations for sub-expressions that must be evaluated on the server
    ///   2) a expression that describes how to combine/project the columns back together into the correct result
    /// </summary>
    public class ColumnProjector : DbExpressionVisitor
    {
        private readonly QueryLanguage _language;
        private readonly Dictionary<ColumnExpression, ColumnExpression> _map;
        private readonly List<ColumnDeclaration> _columns;
        private readonly HashSet<string> _columnNames;
        private readonly HashSet<Expression> _candidates;
        private readonly HashSet<TableAlias> _existingAliases;
        private readonly TableAlias _newAlias;
        private int _iColumn;

        private ColumnProjector(QueryLanguage language, ProjectionAffinity affinity, Expression expression, IEnumerable<ColumnDeclaration> existingColumns, TableAlias newAlias, IEnumerable<TableAlias> existingAliases)
        {
            _language = language;
            _newAlias = newAlias;
            _existingAliases = new HashSet<TableAlias>(existingAliases);
            _map = new Dictionary<ColumnExpression, ColumnExpression>();
            if (existingColumns != null)
            {
                _columns = new List<ColumnDeclaration>(existingColumns);
                _columnNames = new HashSet<string>(existingColumns.Select(c => c.Name));
            }
            else
            {
                _columns = new List<ColumnDeclaration>();
                _columnNames = new HashSet<string>();
            }
            _candidates = Nominator.Nominate(language, affinity, expression);
        }

        public static ProjectedColumns ProjectColumns(QueryLanguage language, ProjectionAffinity affinity, Expression expression, IEnumerable<ColumnDeclaration> existingColumns, TableAlias newAlias, IEnumerable<TableAlias> existingAliases)
        {
            var projector = new ColumnProjector(language, affinity, expression, existingColumns, newAlias, existingAliases);
            var expr = projector.Visit(expression);
            return new ProjectedColumns(expr, projector._columns.AsReadOnly());
        }

        public static ProjectedColumns ProjectColumns(QueryLanguage language, Expression expression, IEnumerable<ColumnDeclaration> existingColumns, TableAlias newAlias, IEnumerable<TableAlias> existingAliases)
        {
            return ProjectColumns(language, ProjectionAffinity.Client, expression, existingColumns, newAlias, existingAliases);
        }

        public static ProjectedColumns ProjectColumns(QueryLanguage language, ProjectionAffinity affinity, Expression expression, IEnumerable<ColumnDeclaration> existingColumns, TableAlias newAlias, params TableAlias[] existingAliases)
        {
            return ProjectColumns(language, affinity, expression, existingColumns, newAlias, (IEnumerable<TableAlias>)existingAliases);
        }

        public static ProjectedColumns ProjectColumns(QueryLanguage language, Expression expression, IEnumerable<ColumnDeclaration> existingColumns, TableAlias newAlias, params TableAlias[] existingAliases)
        {
            return ProjectColumns(language, expression, existingColumns, newAlias, (IEnumerable<TableAlias>)existingAliases);
        }

        protected override Expression Visit(Expression expression)
        {
            if (_candidates.Contains(expression))
            {
                if (expression.NodeType == (ExpressionType)DbExpressionType.Column)
                {
                    var column = (ColumnExpression)expression;
                    ColumnExpression mapped;
                    if (_map.TryGetValue(column, out mapped))
                    {
                        return mapped;
                    }
                    // check for column that already refers to this column
                    foreach (var existingColumn in _columns)
                    {
                        var cex = existingColumn.Expression as ColumnExpression;
                        if (cex != null && cex.Alias == column.Alias && cex.Name == column.Name)
                        {
                            // refer to the column already in the column list
                            return new ColumnExpression(column.Type, column.QueryType, _newAlias, existingColumn.Name);
                        }
                    }
                    if (_existingAliases.Contains(column.Alias)) 
                    {
                        var ordinal = _columns.Count;
                        var columnName = GetUniqueColumnName(column.Name);
                        _columns.Add(new ColumnDeclaration(columnName, column, column.QueryType));
                        mapped = new ColumnExpression(column.Type, column.QueryType, _newAlias, columnName);
                        _map.Add(column, mapped);
                        _columnNames.Add(columnName);
                        return mapped;
                    }
                    // must be referring to outer scope
                    return column;
                }

                {
                    var columnName = GetNextColumnName();
                    var colType = _language.TypeSystem.GetColumnType(expression.Type);
                    _columns.Add(new ColumnDeclaration(columnName, expression, colType));
                    return new ColumnExpression(expression.Type, colType, _newAlias, columnName);
                }
            }

            return base.Visit(expression);
        }

        private bool IsColumnNameInUse(string name)
        {
            return _columnNames.Contains(name);
        }

        private string GetUniqueColumnName(string name)
        {
            var baseName = name;
            var suffix = 1;
            while (IsColumnNameInUse(name))
            {
                name = baseName + (suffix++);
            }
            return name;
        }

        private string GetNextColumnName()
        {
            return GetUniqueColumnName("c" + (_iColumn++));
        }

        /// <summary>
        /// Nominator is a class that walks an expression tree bottom up, determining the set of 
        /// candidate expressions that are possible columns of a select expression
        /// </summary>
        private class Nominator : DbExpressionVisitor
        {
            private readonly QueryLanguage _language;
            private readonly HashSet<Expression> _candidates;
            private readonly ProjectionAffinity _affinity;
            private bool _isBlocked;

            private Nominator(QueryLanguage language, ProjectionAffinity affinity)
            {
                _language = language;
                _affinity = affinity;
                _candidates = new HashSet<Expression>();
                _isBlocked = false;
            }

            internal static HashSet<Expression> Nominate(QueryLanguage language, ProjectionAffinity affinity, Expression expression)
            {
                var nominator = new Nominator(language, affinity);
                nominator.Visit(expression);
                return nominator._candidates;
            }

            protected override Expression Visit(Expression expression)
            {
                if (expression != null)
                {
                    var saveIsBlocked = _isBlocked;
                    _isBlocked = false;
                    if (_language.MustBeColumn(expression))
                    {
                        _candidates.Add(expression);
                        // don't merge saveIsBlocked
                    }
                    else
                    {
                        base.Visit(expression);
                        if (!_isBlocked)
                        {
                            if (_language.MustBeColumn(expression) 
                                || (_affinity == ProjectionAffinity.Server && _language.CanBeColumn(expression)))
                            {
                                _candidates.Add(expression);
                            }
                            else 
                            {
                                _isBlocked = true;
                            }
                        }
                        _isBlocked |= saveIsBlocked;
                    }
                }
                return expression;
            }

            protected override Expression VisitProjection(ProjectionExpression proj)
            {
                Visit(proj.Projector);
                return proj;
            }
        }
    }
}
