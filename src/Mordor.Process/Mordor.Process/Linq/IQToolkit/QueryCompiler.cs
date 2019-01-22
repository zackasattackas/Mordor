// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Mordor.Process.Linq.IQToolkit
{
    /// <summary>
    /// Creates a reusable, parameterized representation of a query that caches the execution plan
    /// </summary>
    public static class QueryCompiler
    {
        public static Delegate Compile(LambdaExpression query)
        {
            var cq = new CompiledQuery(query);
            return StrongDelegate.CreateDelegate(query.Type, cq.Invoke);
        }

        public static TD Compile<TD>(Expression<TD> query)
        {
            return (TD)(object)Compile((LambdaExpression)query);
        }

        public static Func<TResult> Compile<TResult>(Expression<Func<TResult>> query)
        {
            return new CompiledQuery(query).Invoke<TResult>;
        }

        public static Func<T1, TResult> Compile<T1, TResult>(Expression<Func<T1, TResult>> query)
        {
            return new CompiledQuery(query).Invoke<T1, TResult>;
        }

        public static Func<T1, T2, TResult> Compile<T1, T2, TResult>(Expression<Func<T1, T2, TResult>> query)
        {
            return new CompiledQuery(query).Invoke<T1, T2, TResult>;
        }

        public static Func<T1, T2, T3, TResult> Compile<T1, T2, T3, TResult>(Expression<Func<T1, T2, T3, TResult>> query)
        {
            return new CompiledQuery(query).Invoke<T1, T2, T3, TResult>;
        }

        public static Func<T1, T2, T3, T4, TResult> Compile<T1, T2, T3, T4, TResult>(Expression<Func<T1, T2, T3, T4, TResult>> query)
        {
            return new CompiledQuery(query).Invoke<T1, T2, T3, T4, TResult>;
        }

        public static Func<IEnumerable<T>> Compile<T>(this IQueryable<T> source)
        {
            return Compile(
                Expression.Lambda<Func<IEnumerable<T>>>(source.Expression)
                );
        }

        public class CompiledQuery
        {
            private Delegate _fnQuery;

            internal CompiledQuery(LambdaExpression query)
            {
                Query = query;
            }

            public LambdaExpression Query { get; }

            internal void Compile(params object[] args)
            {
                if (_fnQuery == null)
                {
                    // first identify the query provider being used
                    var body = Query.Body;

                    // ask the query provider to compile the query by 'executing' the lambda expression
                    var provider = FindProvider(body, args);
                    if (provider == null)
                    {
                        throw new InvalidOperationException("Could not find query provider");
                    }

                    var result = (Delegate)provider.Execute(Query);
                    Interlocked.CompareExchange(ref _fnQuery, result, null);
                }
            }

            internal IQueryProvider FindProvider(Expression expression, object[] args)
            {
                Expression root = FindProviderInExpression(expression) as ConstantExpression;
                if (root == null && args != null && args.Length > 0)
                {
                    var replaced = ExpressionReplacer.ReplaceAll(
                        expression,
                        Query.Parameters.ToArray(),
                        args.Select((a, i) => Expression.Constant(a, Query.Parameters[i].Type)).ToArray()
                        );
                    root = FindProviderInExpression(replaced);
                }
                if (root != null) 
                {
                    var cex = root as ConstantExpression;
                    if (cex == null)
                    {
                        cex = PartialEvaluator.Eval(root) as ConstantExpression;
                    }
                    if (cex != null)
                    {
                        var provider = cex.Value as IQueryProvider;
                        if (provider == null)
                        {
                            var query = cex.Value as IQueryable;
                            if (query != null)
                            {
                                provider = query.Provider;
                            }
                        }
                        return provider;
                    }
                }
                return null;
            }

            private Expression FindProviderInExpression(Expression expression)
            {
                var root = TypedSubtreeFinder.Find(expression, typeof(IQueryProvider));
                if (root == null)
                {
                    root = TypedSubtreeFinder.Find(expression, typeof(IQueryable));
                }
                return root;
            }

            public object Invoke(object[] args)
            {
                Compile(args);
                if (_invoker == null)
                {
                    _invoker = GetInvoker();
                }
                if (_invoker != null)
                {
                    return _invoker(args);
                }

                try
                {
                    return _fnQuery.DynamicInvoke(args);
                }
                catch (TargetInvocationException tie)
                {
                    throw tie.InnerException;
                }
            }

            private Func<object[], object> _invoker;
            private bool _checkedForInvoker;

            private Func<object[], object> GetInvoker()
            {
                if (_fnQuery != null && _invoker == null && !_checkedForInvoker)
                {
                    _checkedForInvoker = true;
                    var fnType = _fnQuery.GetType();
                    if (fnType.FullName.StartsWith("System.Func`"))
                    {
                        var typeArgs = fnType.GetGenericArguments();
                        var method = GetType().GetMethod("FastInvoke"+typeArgs.Length, BindingFlags.Public|BindingFlags.Instance);
                        if (method != null)
                        {
                            _invoker = (Func<object[], object>)Delegate.CreateDelegate(typeof(Func<object[], object>), this, method.MakeGenericMethod(typeArgs));
                        }
                    }
                }
                return _invoker;
            }

            public object FastInvoke1<TR>(object[] args)
            {
                return ((Func<TR>)_fnQuery)();
            }

            public object FastInvoke2<TA1, TR>(object[] args)
            {
                return ((Func<TA1, TR>)_fnQuery)((TA1)args[0]);
            }

            public object FastInvoke3<TA1, TA2, TR>(object[] args)
            {
                return ((Func<TA1, TA2, TR>)_fnQuery)((TA1)args[0], (TA2)args[1]);
            }

            public object FastInvoke4<TA1, TA2, TA3, TR>(object[] args)
            {
                return ((Func<TA1, TA2, TA3, TR>)_fnQuery)((TA1)args[0], (TA2)args[1], (TA3)args[2]);
            }

            public object FastInvoke5<TA1, TA2, TA3, TA4, TR>(object[] args)
            {
                return ((Func<TA1, TA2, TA3, TA4, TR>)_fnQuery)((TA1)args[0], (TA2)args[1], (TA3)args[2], (TA4)args[3]);
            }

            internal TResult Invoke<TResult>()
            {
                Compile(null);
                return ((Func<TResult>)_fnQuery)();
            }

            internal TResult Invoke<T1, TResult>(T1 arg)
            {
                Compile(arg);
                return ((Func<T1, TResult>)_fnQuery)(arg);
            }

            internal TResult Invoke<T1, T2, TResult>(T1 arg1, T2 arg2)
            {
                Compile(arg1, arg2);
                return ((Func<T1, T2, TResult>)_fnQuery)(arg1, arg2);
            }

            internal TResult Invoke<T1, T2, T3, TResult>(T1 arg1, T2 arg2, T3 arg3)
            {
                Compile(arg1, arg2, arg3);
                return ((Func<T1, T2, T3, TResult>)_fnQuery)(arg1, arg2, arg3);
            }

            internal TResult Invoke<T1, T2, T3, T4, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            {
                Compile(arg1, arg2, arg3, arg4);
                return ((Func<T1, T2, T3, T4, TResult>)_fnQuery)(arg1, arg2, arg3, arg4);
            }
        }
    }
}