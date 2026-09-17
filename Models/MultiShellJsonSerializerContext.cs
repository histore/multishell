using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MultiShell.Models;

/// <summary>
/// Source-generated JSON serializer context ensuring trim-safety and zero-reflection serialization.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(WorkspaceState))]
[JsonSerializable(typeof(TabState))]
[JsonSerializable(typeof(List<TabState>))]
[JsonSerializable(typeof(TerminalProfile))]
[JsonSerializable(typeof(List<TerminalProfile>))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
[JsonSerializable(typeof(List<string>))]
public partial class MultiShellJsonSerializerContext : JsonSerializerContext
{
}
