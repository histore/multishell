using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MultiShell.Services;

public sealed partial class ShellSession
{
    private static readonly Regex Osc9Regex = new(@"\x1b\]9;9;""?([^""\x1b\x07]+)""?(\x1b\\|\x07)", RegexOptions.Compiled);
    private static readonly Regex Osc7Regex = new(@"\x1b\]7;file://[^/\x1b\x07]*/?([^\x1b\x07]+)(\x1b\\|\x07)", RegexOptions.Compiled);
    private static readonly Regex Osc133ERegex = new(@"\x1b\]133;E;([A-Za-z0-9+/=]+)\x07|\x1b\]133;E;([A-Za-z0-9+/=]+)\x1b\\", RegexOptions.Compiled);

    private readonly StringBuilder _oscBuffer = new();
    private readonly Decoder _oscDecoder = Encoding.UTF8.GetDecoder();

    private void PumpOutput(Stream stream, CancellationToken ct)
    {
        byte[] buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;
                var data = buffer.AsSpan(0, bytesRead).ToArray();
                CheckForOscSequences(data);
                DataReceived?.Invoke(data);
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
    }

    private void CheckForOscSequences(byte[] data)
    {
        try
        {
            lock (_oscBuffer)
            {
                int charCount = _oscDecoder.GetCharCount(data, 0, data.Length, flush: false);
                if (charCount <= 0) return;

                char[] chars = new char[charCount];
                _oscDecoder.GetChars(data, 0, data.Length, chars, 0, flush: false);
                _oscBuffer.Append(chars);

                var currentBuffer = _oscBuffer.ToString();
                int lastProcessedIndex = ProcessOscMatches(currentBuffer);

                if (lastProcessedIndex > 0)
                {
                    _oscBuffer.Remove(0, lastProcessedIndex);
                }
                else if (_oscBuffer.Length > 8192)
                {
                    _oscBuffer.Remove(0, 4096);
                }
            }
        }
        catch { }
    }

    private int ProcessOscMatches(string currentBuffer)
    {
        int lastProcessedIndex = -1;

        foreach (Match match in Osc133ERegex.Matches(currentBuffer))
        {
            TryHandleOsc133EMatch(match);
            lastProcessedIndex = Math.Max(lastProcessedIndex, match.Index + match.Length);
        }

        var matches9 = Osc9Regex.Matches(currentBuffer);
        if (matches9.Count > 0)
        {
            UpdateDirectory(matches9[^1].Groups[1].Value.Trim());
            lastProcessedIndex = Math.Max(lastProcessedIndex, matches9[^1].Index + matches9[^1].Length);
        }

        var matches7 = Osc7Regex.Matches(currentBuffer);
        if (matches7.Count > 0)
        {
            UpdateDirectory(Uri.UnescapeDataString(matches7[^1].Groups[1].Value.Trim()));
            lastProcessedIndex = Math.Max(lastProcessedIndex, matches7[^1].Index + matches7[^1].Length);
        }

        return lastProcessedIndex;
    }

    private void TryHandleOsc133EMatch(Match match)
    {
        string base64 = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim();
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var cmd = Encoding.UTF8.GetString(bytes).Trim();
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                _lastExecutedCommand = cmd;
                CommandExecuted?.Invoke(cmd);
            }
        }
        catch { }
    }

    private void UpdateDirectory(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !string.Equals(WorkingDirectory, path, StringComparison.OrdinalIgnoreCase))
        {
            WorkingDirectory = path;
            WorkingDirectoryChanged?.Invoke(path);
        }
    }
}
