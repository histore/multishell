using System;
using System.Collections.Generic;
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
    Task AddProfileAsync(TerminalProfile profile);

    /// <summary>
    /// Updates an existing terminal profile and persists the changes.
    /// </summary>
    Task UpdateProfileAsync(TerminalProfile profile);

    /// <summary>
    /// Deletes a terminal profile and persists the changes.
    /// </summary>
    Task<bool> DeleteProfileAsync(Guid id);

    /// <summary>
    /// Resets all profiles to detected system defaults.
    /// </summary>
    Task ResetToDefaultsAsync();

    /// <summary>
    /// Loads profiles from storage or initializes defaults.
    /// </summary>
    Task LoadProfilesAsync();

    /// <summary>
    /// Event triggered when the profiles list is modified.
    /// </summary>
    event Action? ProfilesChanged;
}
