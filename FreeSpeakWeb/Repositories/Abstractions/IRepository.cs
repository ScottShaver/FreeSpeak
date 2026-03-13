namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Base repository interface providing common CRUD operations
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    public interface IRepository<TEntity> where TEntity : class
    {
        /// <summary>
        /// Get an entity by its ID
        /// </summary>
        Task<TEntity?> GetByIdAsync(int id);

        /// <summary>
        /// Get all entities
        /// </summary>
        Task<List<TEntity>> GetAllAsync();

        /// <summary>
        /// Add a new entity
        /// </summary>
        Task<TEntity> AddAsync(TEntity entity);

        /// <summary>
        /// Update an existing entity
        /// </summary>
        Task UpdateAsync(TEntity entity);

        /// <summary>
        /// Delete an entity
        /// </summary>
        Task DeleteAsync(TEntity entity);

        /// <summary>
        /// Check if an entity exists by ID
        /// </summary>
        Task<bool> ExistsAsync(int id);
    }
}
