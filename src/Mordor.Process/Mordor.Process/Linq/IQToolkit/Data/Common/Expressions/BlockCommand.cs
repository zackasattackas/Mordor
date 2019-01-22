using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class BlockCommand : CommandExpression
    {
        public BlockCommand(IList<Expression> commands)
            : base(DbExpressionType.Block, commands[commands.Count-1].Type)
        {
            Commands = commands.ToReadOnly();
        }

        public BlockCommand(params Expression[] commands) 
            : this((IList<Expression>)commands)
        {
        }

        public ReadOnlyCollection<Expression> Commands { get; }
    }
}