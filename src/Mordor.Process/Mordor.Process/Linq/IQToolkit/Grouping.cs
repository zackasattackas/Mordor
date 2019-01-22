// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Mordor.Process.Linq.IQToolkit
{
    /// <summary>
    /// Simple implementation of the <see cref="IGrouping{TKey, TElement}"/> interface
    /// </summary>
    public class Grouping<TKey, TElement> : IGrouping<TKey, TElement>
    {
        private IEnumerable<TElement> _group;

        public Grouping(TKey key, IEnumerable<TElement> group)
        {
            Key = key;
            _group = group;
        }

        public TKey Key { get; }

        public IEnumerator<TElement> GetEnumerator()
        {
            if (!(_group is List<TElement>))
                _group = _group.ToList();
            return _group.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _group.GetEnumerator();
        }
    }   
}