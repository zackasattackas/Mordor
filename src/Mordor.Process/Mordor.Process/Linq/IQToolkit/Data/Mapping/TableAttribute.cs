using System;

namespace Mordor.Process.Linq.IQToolkit.Data.Mapping
{
    [AttributeUsage(AttributeTargets.Property|AttributeTargets.Field)]
    public class TableAttribute : TableBaseAttribute
    {
        public Type EntityType { get; set; }
    }
}