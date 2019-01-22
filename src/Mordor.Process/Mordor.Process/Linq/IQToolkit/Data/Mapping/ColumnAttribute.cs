using System;

namespace Mordor.Process.Linq.IQToolkit.Data.Mapping
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class ColumnAttribute : MemberAttribute
    {
        public string Name { get; set; }
        public string Alias { get; set; }
        public string DbType { get; set; }
        public bool IsComputed { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsGenerated { get; set; }
        public bool IsReadOnly { get; set; }  
    }
}