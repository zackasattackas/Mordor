using System;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Mapping
{
    public abstract class MappingEntity
    {
        public abstract string TableId { get; }
        public abstract Type ElementType { get; }
        public abstract Type EntityType { get; }
    }
}