namespace SharpArch.RavenDb
{
    using Raven.Client;

    using SharpArch.Domain.DomainModel;
    using SharpArch.Domain.PersistenceSupport;
    using SharpArch.RavenDb.Contracts.Repositories;

    public class RavenDbRepository<T> : RavenDbRepositoryWithTypedId<T, System.Guid>,
        IRavenDbRepository<T>,
        ILinqRepository<T>
    {
        public RavenDbRepository(IDocumentSession session) : base(session)
        {
        }
    }
}