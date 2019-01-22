// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Reflection;

namespace Mordor.Process.Linq.IQToolkit
{
    /// <summary>
    /// Make a strongly-typed delegate to a weakly typed method (one that takes single object[] argument)
    /// (up to 8 arguments)
    /// </summary>
    public class StrongDelegate
    {
        private readonly Func<object[], object> _fn;

        private StrongDelegate(Func<object[], object> fn)
        {
            _fn = fn;
        }

        private static readonly MethodInfo[] _meths;

        static StrongDelegate()
        {
            _meths = new MethodInfo[9];

            var meths = typeof(StrongDelegate).GetMethods();
            for (int i = 0, n = meths.Length; i < n; i++)
            {
                var gm = meths[i];
                if (gm.Name.StartsWith("M"))
                {
                    var tas = gm.GetGenericArguments();
                    _meths[tas.Length - 1] = gm;
                }
            }
        }

        /// <summary>
        /// Create a strongly typed delegate over a method with a weak signature
        /// </summary>
        /// <param name="delegateType">The strongly typed delegate's type</param>
        /// <param name="target"></param>
        /// <param name="method">Any method that takes a single array of objects and returns an object.</param>
        /// <returns></returns>
        public static Delegate CreateDelegate(Type delegateType, object target, MethodInfo method)
        {
            return CreateDelegate(delegateType, (Func<object[], object>)Delegate.CreateDelegate(typeof(Func<object[], object>), target, method));
        }

        /// <summary>
        /// Create a strongly typed delegate over a Func delegate with weak signature
        /// </summary>
        /// <param name="delegateType"></param>
        /// <param name="fn"></param>
        /// <returns></returns>
        public static Delegate CreateDelegate(Type delegateType, Func<object[], object> fn)
        {
            var invoke = delegateType.GetMethod("Invoke");
            var parameters = invoke.GetParameters();
            var typeArgs = new Type[1 + parameters.Length];
            for (int i = 0, n = parameters.Length; i < n; i++)
            {
                typeArgs[i] = parameters[i].ParameterType;
            }
            typeArgs[typeArgs.Length - 1] = invoke.ReturnType;
            if (typeArgs.Length <= _meths.Length)
            {
                var gm = _meths[typeArgs.Length - 1];
                var m = gm.MakeGenericMethod(typeArgs);
                return Delegate.CreateDelegate(delegateType, new StrongDelegate(fn), m);
            }
            throw new NotSupportedException("Delegate has too many arguments");
        }

        public TR M<TR>()
        {
            return (TR)_fn(null);
        }

        public TR M<TA1, TR>(TA1 a1)
        {
            return (TR)_fn(new object[] { a1 });
        }

        public TR M<TA1, TA2, TR>(TA1 a1, TA2 a2)
        {
            return (TR)_fn(new object[] { a1, a2 });
        }

        public TR M<TA1, TA2, TA3, TR>(TA1 a1, TA2 a2, TA3 a3)
        {
            return (TR)_fn(new object[] { a1, a2, a3 });
        }

        public TR M<TA1, TA2, TA3, TA4, TR>(TA1 a1, TA2 a2, TA3 a3, TA4 a4)
        {
            return (TR)_fn(new object[] { a1, a2, a3, a4 });
        }

        public TR M<TA1, TA2, TA3, TA4, TA5, TR>(TA1 a1, TA2 a2, TA3 a3, TA4 a4, TA5 a5)
        {
            return (TR)_fn(new object[] { a1, a2, a3, a4, a5 });
        }

        public TR M<TA1, TA2, TA3, TA4, TA5, TA6, TR>(TA1 a1, TA2 a2, TA3 a3, TA4 a4, TA5 a5, TA6 a6)
        {
            return (TR)_fn(new object[] { a1, a2, a3, a4, a5, a6 });
        }

        public TR M<TA1, TA2, TA3, TA4, TA5, TA6, TA7, TR>(TA1 a1, TA2 a2, TA3 a3, TA4 a4, TA5 a5, TA6 a6, TA7 a7)
        {
            return (TR)_fn(new object[] { a1, a2, a3, a4, a5, a6, a7 });
        }

        public TR M<TA1, TA2, TA3, TA4, TA5, TA6, TA7, TA8, TR>(TA1 a1, TA2 a2, TA3 a3, TA4 a4, TA5 a5, TA6 a6, TA7 a7, TA8 a8)
        {
            return (TR)_fn(new object[] { a1, a2, a3, a4, a5, a6, a7, a8 });
        }
    }
}