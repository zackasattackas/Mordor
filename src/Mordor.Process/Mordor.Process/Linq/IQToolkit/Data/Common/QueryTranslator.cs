// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;
using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;

namespace Mordor.Process.Linq.IQToolkit.Data.Common
{
    /// <summary>
    /// Defines query execution and materialization policies. 
    /// </summary>
    public class QueryTranslator
    {
        public QueryTranslator(QueryLanguage language, QueryMapping mapping, QueryPolicy policy)
        {
            Linguist = language.CreateLinguist(this);
            Mapper = mapping.CreateMapper(this);
            Police = policy.CreatePolice(this);
        }

        public QueryLinguist Linguist { get; }

        public QueryMapper Mapper { get; }

        public QueryPolice Police { get; }

        public virtual Expression Translate(Expression expression)
        {
            // pre-evaluate local sub-trees
            expression = PartialEvaluator.Eval(expression, Mapper.Mapping.CanBeEvaluatedLocally);

            // apply mapping (binds LINQ operators too)
            expression = Mapper.Translate(expression);

            // any policy specific translations or validations
            expression = Police.Translate(expression);

            // any language specific translations or validations
            expression = Linguist.Translate(expression);

            return expression;
        }
    }
}