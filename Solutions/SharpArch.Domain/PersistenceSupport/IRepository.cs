using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpArch.Domain.PersistenceSupport
{
    /// <summary>
    ///     Defines the public members of a class that implements the repository pattern for entities
    ///     of the specified type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    public interface IRepository<T> : ICollection<T>, IQueryable<T>
    {
        /// <summary>
        ///     Get the specified entity
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <param name="id">The entity to load</param>
        T this[object id] { get; }
    }
}