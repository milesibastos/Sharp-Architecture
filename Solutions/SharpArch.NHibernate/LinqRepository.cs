namespace SharpArch.NHibernate
{
    using SharpArch.Domain.PersistenceSupport;

    public class LinqRepository<T> : LinqRepositoryWithTypedId<T, System.Guid>, ILinqRepository<T>
    {
    }
}