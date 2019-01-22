namespace Mordor.Process.Linq.IQToolkit.Data.Common.Mapping
{
    public struct EntityInfo
    {
        public EntityInfo(object instance, MappingEntity mapping)
        {
            Instance = instance;
            Mapping = mapping;
        }

        public object Instance { get; }

        public MappingEntity Mapping { get; }
    }
}