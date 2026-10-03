using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MultiShell.Models;

namespace MultiShell.Services;

/// <summary>
/// Service contract for managing, loading, and persisting terminal profiles.
/// </summary>
public interface ITerminalProfileService
{
    /// <summary>
    /// Gets all configured terminal profiles.
    /// </summary>
    IReadOnlyList<TerminalProfile> GetProfiles();

    /// <summary>
    /// Gets a profile by its unique ID.
    /// </summary>
    TerminalProfile? GetProfile(Guid id);

    /// <summary>
    /// Adds a new terminal profile and persists the changes.
    /// </summary>
    Task AddProfileAsync(TerminalProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing terminal profile and persists the changes.
    /// </summary>
    Task UpdateProfileAsync(TerminalProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a terminal profile and persists the changes.
    /// </summary>
    Task<bool> DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all profiles to detected system defaults.
    /// </summary>
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads profiles from storage or initializes defaults.
    /// </summary>
    Task LoadProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event triggered when the profiles list is modified.
    /// </summary>
    event Action? ProfilesChanged;
}
