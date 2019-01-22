// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Collections.Generic;
using System.Linq;

namespace Mordor.Process.Linq.IQToolkit
{
    public struct DeferredValue<T> : IDeferLoadable
    {
        private IEnumerable<T> _source;
        private T _value;

        public DeferredValue(T value)
        {
            _value = value;
            _source = null;
            IsLoaded = true;
        }

        public DeferredValue(IEnumerable<T> source)
        {
            _source = source;
            IsLoaded = false;
            _value = default;
        }

        public void Load()
        {
            if (_source != null)
            {
                _value = _source.SingleOrDefault();
                IsLoaded = true;
            }
        }

        public bool IsLoaded { get; private set; }

        public bool IsAssigned => IsLoaded && _source == null;

        private void Check()
        {
            if (!IsLoaded)
            {
                Load();
            }
        }

        public T Value
        {
            get
            {
                Check();
                return _value;
            }

            set
            {
                _value = value;
                IsLoaded = true;
                _source = null;
            }
        }
    }
}