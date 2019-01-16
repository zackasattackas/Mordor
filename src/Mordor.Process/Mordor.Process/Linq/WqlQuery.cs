using System;
using System.Linq.Expressions;
using System.Text;

namespace Mordor.Process.Linq
{
    [Obsolete]
    public class WqlQuery
    {
        #region Fields

        private bool _hasWhere;
        private readonly StringBuilder _bldr = new StringBuilder(1024);

        #endregion

        #region Ctor

        public WqlQuery(string className)
        {
            _bldr.Append("SELECT * FROM " + className);
        }

        #endregion

        #region Static methods

        public static WqlQuery Parse(Expression expression)
        {            
            throw new NotImplementedException();
        }

        #endregion

        #region Public methods

        public WqlQuery Where(string filter)
        {
            if (!_hasWhere)
            {
                _bldr.Append("WHERE ");
                _hasWhere = true;
            }

            _bldr.Append(filter);

            return this;
        }        

        public override string ToString()
        {
            return _bldr.ToString();
        }

        #endregion
    }
}