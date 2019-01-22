using System.Linq;

namespace Mordor.Process.Linq.IQToolkit
{
    public interface IEntityTable : IQueryable, IUpdatable
    {
        new IEntityProvider Provider { get; }
        string TableId { get; }
        object GetById(object id);
        int Insert(object instance);
        int Update(object instance);
        int Delete(object instance);
        int InsertOrUpdate(object instance);
    }
}