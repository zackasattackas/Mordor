using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class DeclarationCommand : CommandExpression
    {
        public DeclarationCommand(IEnumerable<VariableDeclaration> variables, SelectExpression source)
            : base(DbExpressionType.Declaration, typeof(void))
        {
            Variables = variables.ToReadOnly();
            Source = source;
        }

        public ReadOnlyCollection<VariableDeclaration> Variables { get; }

        public SelectExpression Source { get; }
    }
}