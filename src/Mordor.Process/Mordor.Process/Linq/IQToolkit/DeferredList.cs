// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;

namespace Mordor.Process.Linq.IQToolkit
{
    public interface IDeferredList<T> : IList<T>, IDeferredList
    {
    }

    /// <summary>
    /// A list implementation that is loaded the first time the contents are examined
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DeferredList<T> : IDeferredList<T>, ICollection<T>, IEnumerable<T>, IList, ICollection, IEnumerable, IDeferLoadable
    {
        private readonly IEnumerable<T> _source;
        private List<T> _values;

        public DeferredList(IEnumerable<T> source)
        {
            _source = source;
        }

        public void Load()
        {
            _values = new List<T>(_source);
        }

        public bool IsLoaded => _values != null;

        private void Check()
        {
            if (!IsLoaded)
            {
                Load();
            }
        }

        #region IList<T> Members

        public int IndexOf(T item)
        {
            Check();
            return _values.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            Check();
            _values.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            Check();
            _values.RemoveAt(index);
        }

        public T this[int index]
        {
            get
            {
                Check();
                return _values[index];
            }
            set
            {
                Check();
                _values[index] = value;
            }
        }

        #endregion

        #region ICollection<T> Members

        public void Add(T item)
        {
            Check();
            _values.Add(item);
        }

        public void Clear()
        {
            Check();
            _values.Clear();
        }

        public bool Contains(T item)
        {
            Check();
            return _values.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Check();
            _values.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { Check(); return _values.Count; }
        }

        public bool IsReadOnly => false;

        public bool Remove(T item)
        {
            Check();
            return _values.Remove(item);
        }

        #endregion

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator()
        {
            Check();
            return _values.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IList Members

        public int Add(object value)
        {
            Check();
            return ((IList)_values).Add(value);
        }

        public bool Contains(object value)
        {
            Check();
            return ((IList)_values).Contains(value);
        }

        public int IndexOf(object value)
        {
            Check();
            return ((IList)_values).IndexOf(value);
        }

        public void Insert(int index, object value)
        {
            Check();
            ((IList)_values).Insert(index, value);
        }

        public bool IsFixedSize => false;

        public void Remove(object value)
        {
            Check();
            ((IList)_values).Remove(value);
        }

        object IList.this[int index]
        {
            get
            {
                Check();
                return ((IList)_values)[index];
            }
            set
            {
                Check();
                ((IList)_values)[index] = value;
            }
        }

        #endregion

        #region ICollection Members

        public void CopyTo(Array array, int index)
        {
            Check();
            ((IList)_values).CopyTo(array, index);
        }

        public bool IsSynchronized => false;

        public object SyncRoot => null;

        #endregion
    }
}