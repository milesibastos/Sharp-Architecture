namespace SharpArch.NHibernate
{

    using System;
    using System.Linq;
    using global::NHibernate;
    using global::NHibernate.Linq;
    using SharpArch.Domain.PersistenceSupport;

    public class NHRepository<T> : BaseRepository<T>
    {
        public NHRepository() : base(NHibernateSession.CurrentFor<T>().Query<T>()) { }

        protected virtual ISession Session
        {
            get { return NHibernateSession.CurrentFor<T>(); }
        }

        public override T this[object id]
        {
            get
            {
                var entity = Session.Get<T>(id);
                return entity;
            }
        }

        public override int Count
        {
            get { return Session.Query<T>().Count(); }
        }

        public override bool Contains(T item)
        {
            return Session.Query<T>().Contains<T>(item);
        }

        public override void CopyTo(T[] array, int arrayIndex)
        {
            var source = Session.Query<T>().ToArray();
            Array.Copy(source, 0, array, arrayIndex, source.Count());
        }

        public override void Add(T item)
        {
            try
            {
                this.Session.Save(item);
            }
            catch
            {
                if (this.Session.IsOpen)
                    this.Session.Close();

                throw;
            }

            this.Session.Flush();
        }

        public override bool Remove(T item)
        {
            Session.Delete(item);
            return true;
        }
    }
}