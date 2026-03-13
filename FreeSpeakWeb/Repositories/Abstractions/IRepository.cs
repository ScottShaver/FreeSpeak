namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Base repository interface providing common CRUD (Create, Read, Update, Delete) operations.
    /// Implements the Repository Pattern to abstract data access logic and provide a clean API
    /// for working with entities in the database.
    /// </summary>
    /// <typeparam name="TEntity">The entity type managed by this repository.</typeparam>
    public interface IRepository<TEntity> where TEntity : class
    {
        /// <summary>
        /// Retrieves an entity by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the entity.</param>
        /// <returns>The entity if found; otherwise, null.</returns>
        Task<TEntity?> GetByIdAsync(int id);

        /// <summary>
        /// Retrieves all entities from the database.
        /// </summary>
        /// <returns>A list of all entities. Returns an empty list if no entities exist.</returns>
        Task<List<TEntity>> GetAllAsync();

        /// <summary>
        /// Adds a new entity to the database.
        /// </summary>
        /// <param name="entity">The entity to add.</param>
        /// <returns>The added entity with any generated values (e.g., ID) populated.</returns>
        Task<TEntity> AddAsync(TEntity entity);

        /// <summary>
        /// Updates an existing entity in the database.
        /// </summary>
        /// <param name="entity">The entity with updated values.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UpdateAsync(TEntity entity);

        /// <summary>
        /// Deletes an entity from the database.
        /// </summary>
        /// <param name="entity">The entity to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteAsync(TEntity entity);

        /// <summary>
        /// Checks whether an entity with the specified ID exists in the database.
        /// </summary>
        /// <param name="id">The unique identifier to check.</param>
        /// <returns>True if the entity exists; otherwise, false.</returns>
        Task<bool> ExistsAsync(int id);
    }
}
