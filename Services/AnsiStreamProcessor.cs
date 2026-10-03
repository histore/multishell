using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MultiShell.Services;

/// <summary>
/// Handles stateful UTF-8 chunk decoding, ANSI/VT100 escape sequence buffering,
/// unsupported OSC sequence sanitization, and terminal OSC 10/11 color query parsing.
/// </summary>
public sealed class AnsiStreamProcessor
{
    private static readonly Regex OscSequenceRegex = new(@"\x1b\][^\x1b\x07]*(\x1b\\|\x07)", RegexOptions.Compiled);
    private static readonly Regex OscColorQueryRegex = new(@"\x1b\](10|11);\?(\x07|\x1b\\)", RegexOptions.Compiled);

    private readonly Decoder _outputDecoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _streamBuffer = new();
    private readonly Lock _syncRoot = new();

    /// <summary>
    /// Processes incoming raw PTY byte chunks, decoding UTF-8 characters and buffering incomplete ANSI/VT sequences.
    /// Detects OSC 10/11 color queries and returns clean sanitized text ready to feed to the terminal control.
    /// </summary>
    public string? ProcessChunk(byte[] data, Action<string>? onOscColorResponse = null, bool isDarkTheme = true)
    {
        if (data.Length == 0) return null;

        lock (_syncRoot)
        {
            int charCount = _outputDecoder.GetCharCount(data, 0, data.Length, flush: false);
            if (charCount == 0) return null;

            char[] chars = new char[charCount];
            _outputDecoder.GetChars(data, 0, data.Length, chars, 0, flush: false);
            _streamBuffer.Append(chars);

            var current = _streamBuffer.ToString();
            _streamBuffer.Clear();

            // 1. Check if there is an incomplete ANSI/VT100 escape sequence at the end of the current stream
            int incompleteIndex = FindIncompleteEscapeSequenceIndex(current);
            if (incompleteIndex >= 0)
            {
                _streamBuffer.Append(current[incompleteIndex..]);
                current = current[..incompleteIndex];
            }

            // 2. Intercept and respond to OSC 10 / OSC 11 color queries (e.g. Neovim background detection)
            if (onOscColorResponse != null)
            {
                DispatchOscColorQueries(current, isDarkTheme, onOscColorResponse);
            }

            // 3. Strip unsupported complete OSC sequences (e.g. OSC 133 / OSC 9;9 shell integration)
            var textToFeed = SanitizeTerminalText(current);

            // Safety boundary on stream buffer: flush remaining sanitized text rather than dropping
            if (_streamBuffer.Length > 16384)
            {
                textToFeed += SanitizeTerminalText(_streamBuffer.ToString());
                _streamBuffer.Clear();
            }

            return string.IsNullOrEmpty(textToFeed) ? null : textToFeed;
        }
    }

    /// <summary>
    /// Dispatches OSC 10 / OSC 11 color query responses for matching sequences in the text.
    /// </summary>
    public static void DispatchOscColorQueries(string text, bool isDarkTheme, Action<string> onResponse)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("\x1b]")) return;

        var matches = OscColorQueryRegex.Matches(text);
        if (matches.Count == 0) return;

        foreach (Match match in matches)
        {
            var code = match.Groups[1].Value;
            string response = code switch
            {
                "11" => isDarkTheme
                    ? "\x1b]11;rgb:0e0e/0f0f/1515\x1b\\"
                    : "\x1b]11;rgb:f8f8/f9f9/fcfc\x1b\\",
                _ => isDarkTheme
                    ? "\x1b]10;rgb:c0c0/caca/f5f5\x1b\\"
                    : "\x1b]10;rgb:1a1a/1d1d/2b2b\x1b\\"
            };

            onResponse(response);
        }
    }

    /// <summary>
    /// Strips unsupported OSC escape sequences (e.g. OSC 8 hyperlinks, OSC 9/133 shell integration)
    /// to prevent terminal controls from misparsing them and printing stray ']' characters at column 0.
    /// </summary>
    public static string SanitizeTerminalText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return OscSequenceRegex.Replace(text, string.Empty);
    }

    /// <summary>
    /// Finds the start index of an incomplete ANSI/VT100 escape sequence at the end of the text stream,
    /// or -1 if the text ends with complete escape sequences / plain characters.
    /// Used to buffer fragmented sequences across stream chunks and prevent color bleeding and render corruption.
    /// </summary>
    public static int FindIncompleteEscapeSequenceIndex(string text)
    {
        if (string.IsNullOrEmpty(text)) return -1;

        int lastEsc = text.LastIndexOf('\x1b');
        if (lastEsc < 0) return -1;

        // If ESC is the very last character in the buffer, it is definitely incomplete
        if (lastEsc == text.Length - 1)
        {
            // Check if this trailing ESC is the beginning of ST (\x1b\) for a preceding unclosed multi-character sequence (OSC, DCS, APC, PM, SOS)
            int lastOsc = text.LastIndexOf("\x1b]", lastEsc, StringComparison.Ordinal);
            int lastDcs = text.LastIndexOf("\x1bP", lastEsc, StringComparison.Ordinal);
            int lastApc = text.LastIndexOf("\x1b_", lastEsc, StringComparison.Ordinal);
            int lastPm = text.LastIndexOf("\x1b^", lastEsc, StringComparison.Ordinal);
            int lastSos = text.LastIndexOf("\x1bX", lastEsc, StringComparison.Ordinal);

            int prevMultiCharEsc = Math.Max(lastOsc, Math.Max(lastDcs, Math.Max(lastApc, Math.Max(lastPm, lastSos))));
            if (prevMultiCharEsc >= 0)
            {
                int bel = text.IndexOf('\x07', prevMultiCharEsc);
                int st = text.IndexOf("\x1b\\", prevMultiCharEsc, StringComparison.Ordinal);
                if (bel < 0 && st < 0)
                {
                    return prevMultiCharEsc;
                }
            }
            return lastEsc;
        }

        char nextChar = text[lastEsc + 1];

        // 1. CSI sequence: \x1b[ ...
        if (nextChar == '[')
        {
            // Scan subsequent characters up to end of string for a final byte (0x40..0x7E, e.g. 'm', 'H', 'K', 'J', 'h', 'l', etc.)
            for (int i = lastEsc + 2; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch >= 0x40 && ch <= 0x7E)
                {
                    // CSI is complete!
                    return -1;
                }
            }
            // No final byte found -> CSI is incomplete
            return lastEsc;
        }

        // 2. OSC sequence: \x1b] ... (terminated by BEL \x07 or ST \x1b\)
        if (nextChar == ']')
        {
            int bel = text.IndexOf('\x07', lastEsc + 2);
            int st = text.IndexOf("\x1b\\", lastEsc + 2, StringComparison.Ordinal);
            if (bel < 0 && st < 0)
            {
                return lastEsc;
            }
            return -1;
        }

        // 3. DCS / APC / PM / SOS sequences (\x1bP, \x1b_, \x1b^, \x1bX) terminated by ST (\x1b\) or BEL (\x07)
        if (nextChar is 'P' or '_' or '^' or 'X')
        {
            int bel = text.IndexOf('\x07', lastEsc + 2);
            int st = text.IndexOf("\x1b\\", lastEsc + 2, StringComparison.Ordinal);
            if (bel < 0 && st < 0)
            {
                return lastEsc;
            }
            return -1;
        }

        // 4. Two-character designation sequences: \x1b(, \x1b), \x1b*, \x1b+, \x1b#, \x1b%
        if (nextChar is '(' or ')' or '*' or '+' or '#' or '%')
        {
            // Requires 1 more character after nextChar
            if (lastEsc + 2 >= text.Length)
            {
                return lastEsc;
            }
            return -1;
        }

        // 5. Standalone 2-character escape sequences (e.g. \x1bM, \x1bD, \x1bE, \x1b7, \x1b8, \x1bc, \x1b=, \x1b>)
        // Since lastEsc < text.Length - 1, the 2nd character is already present.
        return -1;
    }
}
