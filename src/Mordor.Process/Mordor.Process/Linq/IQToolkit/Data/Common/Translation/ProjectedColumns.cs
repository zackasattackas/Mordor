using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Translation
{
    /// <summary>
    /// Result from calling ColumnProjector.ProjectColumns
    /// </summary>
    public sealed class ProjectedColumns
    {
        public ProjectedColumns(Expression projector, ReadOnlyCollection<ColumnDeclaration> columns)
        {
            Projector = projector;
            Columns = columns;
        }

        /// <summary>
        /// The expression to computed on the client.
        /// </summary>
        public Expression Projector { get; }

        /// <summary>
        /// The columns to be computed on the server.
        /// </summary>
        public ReadOnlyCollection<ColumnDeclaration> Columns { get; }
    }
}