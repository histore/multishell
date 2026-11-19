using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using MultiShell.Models;

namespace MultiShell.Services;

public sealed partial class ShellSession
{
    private void StartProcessAttachedToPseudoConsole(WindowsPseudoConsoleSafeHandle pseudoConsole, string? workingDir)
    {
        IntPtr attributeList = IntPtr.Zero;
        bool attributeListInitialized = false;
        IntPtr commandLine = IntPtr.Zero;
        IntPtr environmentBlock = IntPtr.Zero;
        SafeFileHandle? processHandle = null;
        SafeFileHandle? threadHandle = null;

        try
        {
            var size = IntPtr.Zero;
            NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            if (size == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            attributeList = Marshal.AllocHGlobal(size);
            if (!NativeMethods.InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            attributeListInitialized = true;

            if (!NativeMethods.UpdateProcThreadAttribute(attributeList, 0, (IntPtr)NativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, pseudoConsole.DangerousGetHandle(), (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var startupInfo = new StartupInfoEx();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<StartupInfoEx>();
            startupInfo.lpAttributeList = attributeList;

            var (effectiveWorkingDir, shellWorkingDir) = ResolveEffectiveWorkingDirectories(workingDir);
            if (!string.IsNullOrWhiteSpace(effectiveWorkingDir) &&
                (string.IsNullOrWhiteSpace(workingDir) || !workingDir.StartsWith('/')))
            {
                WorkingDirectory = effectiveWorkingDir;
            }

            var rawCommandLine = GenerateShellCommandLine(shellWorkingDir);
            commandLine = Marshal.StringToHGlobalUni(rawCommandLine);

            var envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PYTHONIOENCODING"] = "utf-8",
                ["PYTHONUTF8"] = "1",
                ["LANG"] = "en_US.UTF-8",
                ["LC_ALL"] = "en_US.UTF-8"
            };

            // Only inject Unix-style xterm terminal emulation for WSL/Linux sessions.
            if (_shellType == ShellType.WSL)
            {
                envVars["TERM"] = "xterm-256color";
                envVars["COLORTERM"] = "truecolor";
            }
            foreach (System.Collections.DictionaryEntry de in Environment.GetEnvironmentVariables())
            {
                if (de.Key is string k && de.Value is string v && !envVars.ContainsKey(k))
                    envVars[k] = v;
            }
            var envString = BuildEnvironmentBlock(envVars);
            environmentBlock = Marshal.StringToHGlobalUni(envString);

            var creationFlags = NativeMethods.EXTENDED_STARTUPINFO_PRESENT | NativeMethods.CREATE_UNICODE_ENVIRONMENT;

            if (!NativeMethods.CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, (uint)creationFlags, environmentBlock, effectiveWorkingDir, ref startupInfo, out var processInfo))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            processHandle = new SafeFileHandle(processInfo.hProcess, ownsHandle: true);
            threadHandle = new SafeFileHandle(processInfo.hThread, ownsHandle: true);
            try
            {
                _process = Process.GetProcessById(processInfo.dwProcessId);
            }
            catch (ArgumentException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellSession.StartProcess] Child process {processInfo.dwProcessId} exited immediately: {ex.Message}");
                _process = null;
            }
        }
        finally
        {
            threadHandle?.Dispose();
            processHandle?.Dispose();
            if (attributeList != IntPtr.Zero)
            {
                if (attributeListInitialized)
                {
                    NativeMethods.DeleteProcThreadAttributeList(attributeList);
                }
                Marshal.FreeHGlobal(attributeList);
            }
            if (commandLine != IntPtr.Zero) Marshal.FreeHGlobal(commandLine);
            if (environmentBlock != IntPtr.Zero) Marshal.FreeHGlobal(environmentBlock);
        }
    }

    internal static (string? Win32WorkingDir, string? ShellWorkingDir) ResolveEffectiveWorkingDirectories(string? workingDir)
    {
        if (string.IsNullOrWhiteSpace(workingDir))
        {
            return (null, null);
        }

        if (workingDir.StartsWith('/'))
        {
            return (GetFallbackLocalDirectory(), workingDir);
        }

        if (Directory.Exists(workingDir))
        {
            return (workingDir, workingDir);
        }

        var fallback = GetFallbackLocalDirectory();
        return (fallback, fallback);
    }

    private static string GetFallbackLocalDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile) && Directory.Exists(userProfile))
        {
            return userProfile;
        }
        return Environment.CurrentDirectory;
    }

    internal string GenerateShellCommandLine(string? workingDir)
    {
        if (!string.IsNullOrWhiteSpace(_customExecutable) &&
            _shellType is not (ShellType.PowerShell or ShellType.CMD))
        {
            var args = string.IsNullOrWhiteSpace(_customArguments) ? "" : $" {_customArguments}";
            if (_shellType == ShellType.WSL && !string.IsNullOrWhiteSpace(workingDir) && !args.Contains("--cd"))
            {
                return BuildWslCommandLine(_customExecutable, workingDir, args);
            }
            return $"{FormatWin32QuotedArgument(_customExecutable)}{args}";
        }

        if (_shellType == ShellType.PowerShell)
        {
            string exePath;
            if (!string.IsNullOrWhiteSpace(_customExecutable))
            {
                exePath = _customExecutable;
            }
            else
            {
                exePath = ShellDiscoveryService.ResolveExecutable("pwsh.exe") ??
                          ShellDiscoveryService.ResolveExecutable("powershell.exe") ??
                          Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                if (!File.Exists(exePath)) exePath = "powershell.exe";
            }

            if (HasExplicitPowerShellCommandArgument(_customArguments))
            {
                return $"{FormatWin32QuotedArgument(exePath)} {_customArguments!.Trim()}";
            }

            // Build the prompt hook script as a plain string (no escaping needed here).
            // Explicitly set Win32 console code page (65001), UTF-8 console output/input,
            // and pipeline encoding to guarantee box-drawing characters and unicode glyphs
            // are transmitted correctly through ConPTY across all CLI tools and sub-processes.
            // It also emits OSC 133;E with Base64-encoded last command for history tracking,
            // and OSC 9;9 for working-directory tracking (shell integration).
            const string hookScript = """
                chcp 65001 >$null
                [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
                [Console]::InputEncoding = [System.Text.Encoding]::UTF8
                $OutputEncoding = [System.Text.Encoding]::UTF8
                if (Get-Command Set-PSReadLineOption -ErrorAction SilentlyContinue) {
                    Set-PSReadLineOption -AddToHistoryHandler {
                        param($cmd)
                        if ([string]::IsNullOrWhiteSpace($cmd)) { return $false }
                        $t = $cmd.Trim()
                        if ($t -match '^(chcp(\s|$)|\[Console\]::|\$OutputEncoding|\$function:prompt|Set-PSReadLineOption|__multishell_)') {
                            return $false
                        }
                        return $true
                    }
                }
                $function:prompt = {
                    $success = $?
                    $code = $LASTEXITCODE
                    $h = Get-History -Count 1
                    if ($h -and $h.Id -ne $global:__multishell_last_history_id) {
                        $global:__multishell_last_history_id = $h.Id
                        $last = $h.CommandLine
                        if ($last -and ($last.Trim() -notmatch '^(chcp(\s|$)|\[Console\]::|\$OutputEncoding|\$function:prompt|Set-PSReadLineOption|__multishell_)')) {
                            $lastExit = if ($success) { 0 } else { if ($code -ne $null -and $code -ne 0) { $code } else { 1 } }
                            [Console]::Write([char]27 + ']133;D;' + $lastExit + [char]7)
                            $b = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($last))
                            [Console]::Write([char]27 + ']133;E;' + $b + [char]7)
                        }
                    }
                    $loc = $ExecutionContext.SessionState.Path.CurrentLocation.Path
                    [Console]::Write([char]27 + ']9;9;"' + $loc + '"' + [char]7)
                    "PS $loc$('>' * ($nestedPromptLevel + 1)) "
                }
                """;

            // Encode as UTF-16LE Base64 for -EncodedCommand; avoids all quoting issues.
            string encodedHook = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(hookScript));
            string extraFlags = BuildAdditionalPowerShellFlags(_customArguments);
            return $"{FormatWin32QuotedArgument(exePath)}{extraFlags} -NoLogo -NoExit -EncodedCommand {encodedHook}";
        }

        if (_shellType == ShellType.NuShell)
        {
            string exePath = ShellDiscoveryService.ResolveExecutable("nu.exe") ?? "nu.exe";
            return FormatWin32QuotedArgument(exePath);
        }

        if (_shellType == ShellType.WSL)
        {
            string exePath = ShellDiscoveryService.ResolveExecutable("wsl.exe") ??
                             Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
            if (!File.Exists(exePath)) exePath = "wsl.exe";

            if (!string.IsNullOrWhiteSpace(workingDir))
            {
                return BuildWslCommandLine(exePath, workingDir);
            }
            return FormatWin32QuotedArgument(exePath);
        }

        string cmdPath;
        if (!string.IsNullOrWhiteSpace(_customExecutable))
        {
            cmdPath = _customExecutable;
        }
        else
        {
            cmdPath = ShellDiscoveryService.ResolveExecutable("cmd.exe") ??
                      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            if (!File.Exists(cmdPath)) cmdPath = "cmd.exe";
        }

        if (!string.IsNullOrWhiteSpace(_customArguments))
        {
            return $"{FormatWin32QuotedArgument(cmdPath)} {_customArguments.Trim()}";
        }
        return $"{FormatWin32QuotedArgument(cmdPath)} /K \"chcp 65001 >nul\"";
    }

    private static bool HasExplicitPowerShellCommandArgument(string? customArguments)
    {
        if (string.IsNullOrWhiteSpace(customArguments)) return false;

        foreach (var token in customArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("-Command", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("-c", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("-EncodedCommand", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("-ec", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("-File", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("-f", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string BuildAdditionalPowerShellFlags(string? customArguments)
    {
        if (string.IsNullOrWhiteSpace(customArguments)) return string.Empty;

        var filtered = new System.Collections.Generic.List<string>();
        foreach (var token in customArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!token.Equals("-NoLogo", StringComparison.OrdinalIgnoreCase) &&
                !token.Equals("-NoExit", StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(token);
            }
        }

        return filtered.Count > 0 ? " " + string.Join(' ', filtered) : string.Empty;
    }

    internal static string BuildWslCommandLine(string executable, string workingDir, string? extraArgs = null)
    {
        var quotedExe = FormatWin32QuotedArgument(executable);
        var quotedDir = FormatWin32QuotedArgument(Path.TrimEndingDirectorySeparator(workingDir));
        return $"{quotedExe} --cd {quotedDir}{extraArgs}";
    }

    /// <summary>
    /// Quotes a Win32 command-line argument according to CommandLineToArgvW escaping rules
    /// so trailing backslashes (e.g. "C:\") and embedded quotes do not break argument boundaries.
    /// </summary>
    internal static string FormatWin32QuotedArgument(string value)
    {
        var sb = new StringBuilder(value.Length + 4);
        sb.Append('"');

        int backslashCount = 0;
        foreach (char ch in value)
        {
            if (ch == '\\')
            {
                backslashCount++;
                continue;
            }

            if (ch == '"')
            {
                sb.Append('\\', backslashCount * 2 + 1);
                sb.Append('"');
                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                sb.Append('\\', backslashCount);
                backslashCount = 0;
            }

            sb.Append(ch);
        }

        if (backslashCount > 0)
        {
            sb.Append('\\', backslashCount * 2);
        }

        sb.Append('"');
        return sb.ToString();
    }

    private static string BuildEnvironmentBlock(IReadOnlyDictionary<string, string> environmentVariables)
    {
        var builder = new StringBuilder();
        foreach (var entry in environmentVariables.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            builder.Append(entry.Key).Append('=').Append(entry.Value).Append('\0');
        builder.Append('\0');
        return builder.ToString();
    }
}
