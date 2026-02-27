namespace HealthingHand.Data.Stores;

public interface IStore<TE, in TK>
     where TE : class
{
    /// <summary>
    /// Get a single entry by its unique identifier from the database.
    /// </summary>
    /// <param name="id">The ID of the entry to get.</param>
    /// <returns>The entry with the given type and ID, returns null if not found.</returns>
    Task<TE?> GetAsync(TK id);
    
    /// <summary>
    /// Add a new entry to the database.
    /// </summary>
    /// <param name="entry">The entry to be added to the database.</param>
    /// <returns>None</returns>
    Task AddAsync(TE entry);
    
    /// <summary>
    /// Update an existing entry in the database.
    /// The entry must have a valid ID that corresponds to an existing entry in the database.
    /// </summary>
    /// <param name="entry">The entry to be updated.</param>
    /// <returns>None</returns>
    Task UpdateAsync(TE entry);
    
    /// <summary>
    /// Deletes the entry with the given ID from the database.
    /// If no entry with the given ID exists, this method does nothing.
    /// </summary>
    /// <param name="id">The ID of the entry to delete.</param>
    /// <returns>None</returns>
    Task DeleteAsync(TK id);
}