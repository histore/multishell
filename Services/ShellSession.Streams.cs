using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MultiShell.Services;

public sealed partial class ShellSession
{
    [GeneratedRegex(@"\x1b\]9;9;""?([^""\x1b\x07]+)""?(\x1b\\|\x07)")]
    private static partial Regex GetOsc9Regex();

    [GeneratedRegex(@"\x1b\]7;file://[^/\x1b\x07]*(/[^\x1b\x07]+)(\x1b\\|\x07)")]
    private static partial Regex GetOsc7Regex();

    [GeneratedRegex(@"\x1b\]133;E;([A-Za-z0-9+/=]+)\x07|\x1b\]133;E;([A-Za-z0-9+/=]+)\x1b\\")]
    private static partial Regex GetOsc133ERegex();

    [GeneratedRegex(@"\x1b\]133;D;(-?\d+)\x07|\x1b\]133;D;(-?\d+)\x1b\\")]
    private static partial Regex GetOsc133DRegex();

    private readonly StringBuilder _oscBuffer = new();
    private readonly Lock _oscBufferLock = new();
    private readonly Decoder _oscDecoder = Encoding.UTF8.GetDecoder();

    private void PumpOutput(Stream stream, CancellationToken ct)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;
                ReadOnlyMemory<byte> data = buffer.AsMemory(0, bytesRead);
                CheckForOscSequences(data.Span);
                DataReceived?.Invoke(data);
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal void CheckForOscSequences(byte[] data) => CheckForOscSequences(data.AsSpan());

    private void CheckForOscSequences(ReadOnlySpan<byte> data)
    {
        List<int>? completedCodes = null;
        List<string>? executedCommands = null;
        string? directoryUpdate = null;

        try
        {
            lock (_oscBufferLock)
            {
                ProcessOscBufferLocked(data, ref completedCodes, ref executedCommands, ref directoryUpdate);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or DecoderFallbackException or RegexMatchTimeoutException or UriFormatException)
        {
            System.Diagnostics.Debug.WriteLine($"[ShellSession.CheckForOscSequences] Failed to parse OSC sequence: {ex.Message}");
            return;
        }

        // Invoke external event subscribers outside _oscBufferLock to prevent lock-inversion deadlocks
        if (completedCodes != null)
        {
            foreach (var code in completedCodes)
            {
                CommandCompleted?.Invoke(code);
            }
        }

        if (executedCommands != null)
        {
            foreach (var cmd in executedCommands)
            {
                CommandExecuted?.Invoke(cmd);
            }
        }

        if (directoryUpdate != null)
        {
            WorkingDirectoryChanged?.Invoke(directoryUpdate);
        }
    }

    private void ProcessOscBufferLocked(
        ReadOnlySpan<byte> data,
        ref List<int>? completedCodes,
        ref List<string>? executedCommands,
        ref string? directoryUpdate)
    {
        int maxChars = Encoding.UTF8.GetMaxCharCount(data.Length);
        if (maxChars <= 0) return;

        char[] rentedChars = ArrayPool<char>.Shared.Rent(maxChars);
        try
        {
            int actualChars = _oscDecoder.GetChars(data, rentedChars, flush: false);
            if (actualChars <= 0) return;

            var decodedSpan = rentedChars.AsSpan(0, actualChars);

            // Fast-path: if no partial OSC buffer exists and the decoded chunk contains neither an OSC introducer ("\x1b]")
            // nor a trailing ESC byte that could start one on the next chunk, skip StringBuilder allocation and all 4 regex scans.
            if (_oscBuffer.Length == 0 &&
                decodedSpan.IndexOf("\x1b]", StringComparison.Ordinal) < 0 &&
                decodedSpan[^1] != '\x1b')
            {
                return;
            }

            _oscBuffer.Append(rentedChars, 0, actualChars);

            var currentBuffer = _oscBuffer.ToString();
            int lastProcessedIndex = ProcessOscMatches(currentBuffer, ref completedCodes, ref executedCommands, ref directoryUpdate);

            TrimCompletedOscBuffer(currentBuffer, lastProcessedIndex);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rentedChars);
        }
    }

    private void TrimCompletedOscBuffer(string currentBuffer, int lastProcessedIndex)
    {
        var remaining = currentBuffer.AsSpan(Math.Max(0, lastProcessedIndex));
        int keepOffset = FindUnterminatedOscStartIndex(remaining);

        if (keepOffset < 0)
        {
            _oscBuffer.Clear();
            return;
        }

        var tailToKeep = remaining[keepOffset..];
        if (tailToKeep.Length > 4096)
        {
            // Guard against unbounded growth on malformed/unterminated OSC sequences
            _oscBuffer.Clear();
            return;
        }

        int absoluteKeepIndex = Math.Max(0, lastProcessedIndex) + keepOffset;
        if (absoluteKeepIndex > 0)
        {
            _oscBuffer.Remove(0, absoluteKeepIndex);
        }
    }

    private static int FindUnterminatedOscStartIndex(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty) return -1;

        int lastOscIntro = span.LastIndexOf("\x1b]", StringComparison.Ordinal);
        if (lastOscIntro >= 0)
        {
            var afterIntro = span[(lastOscIntro + 2)..];
            bool hasBelTerminator = afterIntro.IndexOf('\x07') >= 0;
            bool hasStTerminator = afterIntro.IndexOf("\x1b\\", StringComparison.Ordinal) >= 0;
            if (!hasBelTerminator && !hasStTerminator)
            {
                return lastOscIntro;
            }
        }

        if (span[^1] == '\x1b')
        {
            return span.Length - 1;
        }

        return -1;
    }

    private int ProcessOscMatches(
        string currentBuffer,
        ref List<int>? completedCodes,
        ref List<string>? executedCommands,
        ref string? directoryUpdate)
    {
        int lastProcessedIndex = -1;

        foreach (Match match in GetOsc133DRegex().Matches(currentBuffer))
        {
            if (TryParseOsc133DMatch(match, out int exitCode))
            {
                (completedCodes ??= new List<int>()).Add(exitCode);
            }
            lastProcessedIndex = Math.Max(lastProcessedIndex, match.Index + match.Length);
        }

        foreach (Match match in GetOsc133ERegex().Matches(currentBuffer))
        {
            if (TryParseOsc133EMatch(match, out var cmd))
            {
                (executedCommands ??= new List<string>()).Add(cmd);
            }
            lastProcessedIndex = Math.Max(lastProcessedIndex, match.Index + match.Length);
        }

        var matches9 = GetOsc9Regex().Matches(currentBuffer);
        if (matches9.Count > 0)
        {
            var candidate = matches9[^1].Groups[1].Value.Trim();
            if (TryApplyDirectoryUpdate(candidate))
            {
                directoryUpdate = candidate;
            }
            lastProcessedIndex = Math.Max(lastProcessedIndex, matches9[^1].Index + matches9[^1].Length);
        }

        var matches7 = GetOsc7Regex().Matches(currentBuffer);
        if (matches7.Count > 0)
        {
            var candidate = NormalizeOsc7Path(Uri.UnescapeDataString(matches7[^1].Groups[1].Value.Trim()));
            if (TryApplyDirectoryUpdate(candidate))
            {
                directoryUpdate = candidate;
            }
            lastProcessedIndex = Math.Max(lastProcessedIndex, matches7[^1].Index + matches7[^1].Length);
        }

        return lastProcessedIndex;
    }

    private static string NormalizeOsc7Path(string path)
    {
        if (path.Length >= 3 && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[2] == ':')
        {
            return path[1..];
        }
        return path;
    }

    private static bool TryParseOsc133DMatch(Match match, out int exitCode)
    {
        string rawCode = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim();
        return int.TryParse(rawCode, out exitCode);
    }

    private static bool TryParseOsc133EMatch(Match match, out string command)
    {
        command = string.Empty;
        string base64 = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim();
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var rawCmd = Encoding.UTF8.GetString(bytes);
            var cmd = SanitizeOscControlCharacters(rawCmd).Trim();
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                command = cmd;
                return true;
            }
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or DecoderFallbackException)
        {
            System.Diagnostics.Debug.WriteLine($"[ShellSession.TryParseOsc133EMatch] Invalid Base64 OSC 133;E payload: {ex.Message}");
        }
        return false;
    }

    private static string SanitizeOscControlCharacters(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch == '\r' || ch == '\n')
            {
                sb.Append(' ');
            }
            else if (!char.IsControl(ch))
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    private bool TryApplyDirectoryUpdate(string path)
    {
        var sanitized = SanitizeOscControlCharacters(path).Trim();
        if (string.IsNullOrWhiteSpace(sanitized)) return false;

        if (!sanitized.StartsWith('/') && !LinkDetectionHelper.IsSafeLocalPath(sanitized))
        {
            return false;
        }

        if (!string.Equals(WorkingDirectory, sanitized, StringComparison.OrdinalIgnoreCase))
        {
            WorkingDirectory = sanitized;
            return true;
        }
        return false;
    }
}
