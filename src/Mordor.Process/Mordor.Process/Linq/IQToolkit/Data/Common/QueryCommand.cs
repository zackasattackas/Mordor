// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mordor.Process.Linq.IQToolkit.Data.Common
{
    public class QueryCommand
    {
        public QueryCommand(string commandText, IEnumerable<QueryParameter> parameters)
        {
            CommandText = commandText;
            Parameters = parameters.ToReadOnly();
        }

        public string CommandText { get; }

        public ReadOnlyCollection<QueryParameter> Parameters { get; }
    }
}
