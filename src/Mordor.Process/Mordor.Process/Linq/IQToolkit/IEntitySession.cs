// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Linq;

namespace Mordor.Process.Linq.IQToolkit
{
    public interface IEntitySession
    {
        IEntityProvider Provider { get; }
        ISessionTable<T> GetTable<T>(string tableId);
        ISessionTable GetTable(Type elementType, string tableId);
        void SubmitChanges();
    }

    public interface ISessionTable<T> : IQueryable<T>, ISessionTable
    {
        new IEntityTable<T> ProviderTable { get; }
        new T GetById(object id);
        void SetSubmitAction(T instance, SubmitAction action);
        SubmitAction GetSubmitAction(T instance);
    }
}