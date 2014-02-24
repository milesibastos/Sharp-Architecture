using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using SharpArch.Domain.DomainModel;

namespace SharpArch.Domain.PersistenceSupport
{
    public abstract class BaseRepository<T> : EnumerableQuery<T>, IRepository<T>
    {
        // Summary:
        //     Initializes a new instance of the System.Linq.EnumerableQuery<T> class and
        //     associates the instance with an expression tree.
        //
        // Parameters:
        //   expression:
        //     An expression tree to associate with the new instance.
        public BaseRepository(Expression expression) : base(expression) { }

        // Summary:
        //     Initializes a new instance of the System.Linq.EnumerableQuery<T> class and
        //     associates it with an System.Collections.Generic.IEnumerable<T> collection.
        //
        // Parameters:
        //   enumerable:
        //     A collection to associate with the new instance.
        public BaseRepository(IEnumerable<T> enumerable) : base(enumerable) { }

        public virtual T this[object id]
        {
            get { return (T)this.Cast<IEntityWithTypedId<object>>().Single(x => x.Id == id); }
        }

        public virtual bool Contains(T item)
        {
            return this.Contains<T>(item);
        }

        public virtual int Count
        {
            get { return this.Count<T>(); }
        }

        public virtual bool IsReadOnly
        {
            get { return false; }
        }

        public virtual void Clear()
        {
            throw new System.NotImplementedException();
        }

        public virtual void CopyTo(T[] array, int arrayIndex)
        {
            var source = ((IEnumerable<T>)this).ToArray();
            Array.Copy(source, 0, array, arrayIndex, source.Count());
        }

        public abstract void Add(T item);

        public abstract bool Remove(T item);
    }
}
