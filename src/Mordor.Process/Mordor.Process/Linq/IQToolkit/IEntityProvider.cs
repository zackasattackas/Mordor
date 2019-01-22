// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Linq;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit
{
    public interface IEntityProvider : IQueryProvider
    {
        IEntityTable<T> GetTable<T>(string tableId);
        IEntityTable GetTable(Type type, string tableId);
        bool CanBeEvaluatedLocally(Expression expression);
        bool CanBeParameter(Expression expression);
    }

    public interface IEntityTable<T> : IQueryable<T>, IEntityTable, IUpdatable<T>
    {
        new T GetById(object id);
        int Insert(T instance);
        int Update(T instance);
        int Delete(T instance);
        int InsertOrUpdate(T instance);
    }
}