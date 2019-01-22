namespace Mordor.Process.Linq.IQToolkit.Data.Common.Language
{
    public abstract class QueryType
    {
        public abstract bool NotNull { get; }
        public abstract int Length { get; }
        public abstract short Precision { get; }
        public abstract short Scale { get; }
    }
}