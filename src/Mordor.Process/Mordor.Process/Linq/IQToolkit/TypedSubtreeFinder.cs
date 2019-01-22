// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit
{
    /// <summary>
    /// Finds the first sub-expression that is of a specified type
    /// </summary>
    public class TypedSubtreeFinder : ExpressionVisitor
    {
        private Expression _root;
        private readonly Type _type;

        private TypedSubtreeFinder(Type type)
        {
            _type = type;
        }

        public static Expression Find(Expression expression, Type type)
        {
            var finder = new TypedSubtreeFinder(type);
            finder.Visit(expression);
            return finder._root;
        }

        protected override Expression Visit(Expression exp)
        {
            var result = base.Visit(exp);

            // remember the first sub-expression that produces an IQueryable
            if (_root == null && result != null)
            {
                if (_type.IsAssignableFrom(result.Type))
                    _root = result;
            }

            return result;
        }
    }
}