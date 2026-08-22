using System.Threading.Tasks;
using MultiShell.Models;

namespace MultiShell.Services;

/// <summary>
/// Service interface for loading and saving open tab workspace states across app restarts.
/// </summary>
public interface ITabStatePersistenceService
{
    /// <summary>
    /// Saves the current workspace state to persistent storage.
    /// </summary>
    Task SaveStateAsync(WorkspaceState state);

    /// <summary>
    /// Loads the previously saved workspace state, or null if no saved state exists or an error occurs.
    /// </summary>
    Task<WorkspaceState?> LoadStateAsync();
}
