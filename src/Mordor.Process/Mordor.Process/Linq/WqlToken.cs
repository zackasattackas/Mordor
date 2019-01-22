namespace Mordor.Process.Linq
{
    internal abstract class WqlToken
    {
        public const string WhiteSpace = " ";
        public const string Select = " SELECT ";
        public const string From = " FROM ";
        public const string Where = " WHERE ";

        public const string Equal = " = ";
        public const string NotEqual = " <> ";
        public const string LessThan = " < ";
        public const string GreaterThan = " > ";
        public const string LessThanOrEqual = " <= ";
        public const string GreaterThanOrEqual = " >= ";
        public const string Is = " IS ";
        public const string IsNot = Is + "  NOT ";
        public const string Like = " LIKE ";

        public const string And = " AND ";
        public const string Or = " OR ";
        public const string Not = " NOT ";

        public const string Null = " NULL ";
        public const string True = " TRUE ";
        public const string False = " FALSE ";

        public const string Star = "*";

        public static string Join(params string[] tokens)
        {
            return string.Join(" ", tokens);
        }
    }
}