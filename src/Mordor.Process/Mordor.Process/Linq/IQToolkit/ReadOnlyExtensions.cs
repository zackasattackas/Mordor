// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mordor.Process.Linq.IQToolkit
{
    public static class ReadOnlyExtensions
    {
        public static ReadOnlyCollection<T> ToReadOnly<T>(this IEnumerable<T> collection)
        {
            var roc = collection as ReadOnlyCollection<T>;
            if (roc == null)
            {
                if (collection == null)
                {
                    roc = EmptyReadOnlyCollection<T>.Empty;
                }
                else
                {
                    roc = new List<T>(collection).AsReadOnly();
                }
            }
            return roc;
        }

        private class EmptyReadOnlyCollection<T>
        {
            internal static readonly ReadOnlyCollection<T> Empty = new List<T>().AsReadOnly();
        }
    }
}
