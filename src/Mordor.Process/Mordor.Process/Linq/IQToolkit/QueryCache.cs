using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit
{
    public class QueryCache
    {
        private readonly MostRecentlyUsedCache<QueryCompiler.CompiledQuery> _cache;
        private static readonly Func<QueryCompiler.CompiledQuery, QueryCompiler.CompiledQuery, bool> FnCompareQueries = CompareQueries;
        private static readonly Func<object, object, bool> FnCompareValues = CompareConstantValues;

        public QueryCache(int maxSize)
        {
            _cache = new MostRecentlyUsedCache<QueryCompiler.CompiledQuery>(maxSize, FnCompareQueries);
        }

        private static bool CompareQueries(QueryCompiler.CompiledQuery x, QueryCompiler.CompiledQuery y)
        {
            return ExpressionComparer.AreEqual(x.Query, y.Query, FnCompareValues);
        }

        private static bool CompareConstantValues(object x, object y)
        {
            if (x == y) return true;
            if (x == null || y == null) return false;
            if (x is IQueryable && y is IQueryable && x.GetType() == y.GetType()) return true;
            return Equals(x, y);
        }

        public object Execute(Expression query)
        {
            object[] args;
            var cached = Find(query, true, out args);
            return cached.Invoke(args);
        }

        public object Execute(IQueryable query)
        {
            return Equals(query.Expression);
        }

        public IEnumerable<T> Execute<T>(IQueryable<T> query)
        {
            return (IEnumerable<T>)Execute(query.Expression);
        }

        public int Count => _cache.Count;

        public void Clear()
        {
            _cache.Clear();
        }

        public bool Contains(Expression query)
        {
            object[] args;
            return Find(query, false, out args) != null;
        }

        public bool Contains(IQueryable query)
        {
            return Contains(query.Expression);
        }

        private QueryCompiler.CompiledQuery Find(Expression query, bool add, out object[] args)
        {
            var pq = Parameterize(query, out args);
            var cq = new QueryCompiler.CompiledQuery(pq);
            QueryCompiler.CompiledQuery cached;
            _cache.Lookup(cq, add, out cached);
            return cached;
        }

        private LambdaExpression Parameterize(Expression query, out object[] arguments)
        {
            var provider = FindProvider(query);
            if (provider == null)
            {
                throw new ArgumentException("Cannot deduce query provider from query");
            }

            var ep = provider as IEntityProvider;
            var fn = ep != null ? (Func<Expression, bool>)ep.CanBeEvaluatedLocally : null;
            var parameters = new List<ParameterExpression>();
            var values = new List<object>();

            var body = PartialEvaluator.Eval(query, fn, c =>
            {
                var isQueryRoot = c.Value is IQueryable;
                if (!isQueryRoot && ep != null && !ep.CanBeParameter(c))
                    return c;
                var p = Expression.Parameter(c.Type, "p" + parameters.Count);
                parameters.Add(p);
                values.Add(c.Value);
                // if query root then parameterize but don't replace in the tree 
                if (isQueryRoot)
                    return c;
                return p;
            });

            if (body.Type != typeof(object))
                body = Expression.Convert(body, typeof(object));

            arguments = values.ToArray();
            if (arguments.Length < 5)
            {
                return Expression.Lambda(body, parameters.ToArray());
            }

            arguments = new object[] { arguments };
            return ExplicitToObjectArray.Rewrite(body, parameters);
        }

        private IQueryProvider FindProvider(Expression expression)
        {
            var root = TypedSubtreeFinder.Find(expression, typeof(IQueryProvider)) as ConstantExpression;
            if (root == null)
            {
                root = TypedSubtreeFinder.Find(expression, typeof(IQueryable)) as ConstantExpression;
            }
            if (root != null)
            {
                var provider = root.Value as IQueryProvider;
                if (provider == null)
                {
                    var query = root.Value as IQueryable;
                    if (query != null)
                    {
                        provider = query.Provider;
                    }
                }
                return provider;
            }
            return null;
        }


        private class ExplicitToObjectArray : ExpressionVisitor
        {
            private readonly IList<ParameterExpression> _parameters;
            private readonly ParameterExpression _array = Expression.Parameter(typeof(object[]), "array");

            private ExplicitToObjectArray(IList<ParameterExpression> parameters)
            {
                _parameters = parameters;
            }

            internal static LambdaExpression Rewrite(Expression body, IList<ParameterExpression> parameters)
            {
                var visitor = new ExplicitToObjectArray(parameters);
                return Expression.Lambda(visitor.Visit(body), visitor._array);                  
            }

            protected override Expression VisitParameter(ParameterExpression p)
            {
                for (int i = 0, n = _parameters.Count; i < n; i++)
                {
                    if (_parameters[i] == p)
                    {
                        return Expression.Convert(Expression.ArrayIndex(_array, Expression.Constant(i)), p.Type);
                    }
                }
                return p;
            }
        }
    }
}
