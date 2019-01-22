using System.Linq;

namespace Mordor.Process.Linq.IQToolkit
{
    public interface ISessionTable : IQueryable
    {
        IEntitySession Session { get; }
        IEntityTable ProviderTable { get; }
        object GetById(object id);
        void SetSubmitAction(object instance, SubmitAction action);
        SubmitAction GetSubmitAction(object instance);
    }
}