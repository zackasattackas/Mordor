namespace Mordor.Process.Linq.IQToolkit.Data.Mapping
{
    public abstract class TableBaseAttribute : MappingAttribute
    {
        public string Name { get; set; }
        public string Alias { get; set; }
    }
}