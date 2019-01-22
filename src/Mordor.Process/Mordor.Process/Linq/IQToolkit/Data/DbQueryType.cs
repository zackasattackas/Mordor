using System.Data;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data
{
    public class DbQueryType : QueryType
    {
        public DbQueryType(SqlDbType dbType, bool notNull, int length, short precision, short scale)
        {
            SqlDbType = dbType;
            NotNull = notNull;
            Length = length;
            Precision = precision;
            Scale = scale;
        }

        public DbType DbType => DbTypeSystem.GetDbType(SqlDbType);

        public SqlDbType SqlDbType { get; }

        public override int Length { get; }

        public override bool NotNull { get; }

        public override short Precision { get; }

        public override short Scale { get; }
    }
}