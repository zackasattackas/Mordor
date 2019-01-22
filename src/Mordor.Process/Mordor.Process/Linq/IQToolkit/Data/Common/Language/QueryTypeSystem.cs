// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Language
{
    public abstract class QueryTypeSystem 
    {
        public abstract QueryType Parse(string typeDeclaration);
        public abstract QueryType GetColumnType(Type type);
        public abstract string GetVariableDeclaration(QueryType type, bool suppressSize);
    }
}