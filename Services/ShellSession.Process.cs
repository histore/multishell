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
        IntPtr commandLine = IntPtr.Zero;
        IntPtr environmentBlock = IntPtr.Zero;
        SafeFileHandle? processHandle = null;
        SafeFileHandle? threadHandle = null;

        try
        {
            var size = IntPtr.Zero;
            NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attributeList = Marshal.AllocHGlobal(size);
            if (!NativeMethods.InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (!NativeMethods.UpdateProcThreadAttribute(attributeList, 0, (IntPtr)NativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, pseudoConsole.DangerousGetHandle(), (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var startupInfo = new StartupInfoEx();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<StartupInfoEx>();
            startupInfo.lpAttributeList = attributeList;

            var rawCommandLine = GenerateShellCommandLine(workingDir);
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

            string? effectiveWorkingDir = workingDir;
            if (!string.IsNullOrWhiteSpace(effectiveWorkingDir) && !Directory.Exists(effectiveWorkingDir))
            {
                effectiveWorkingDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(effectiveWorkingDir) || !Directory.Exists(effectiveWorkingDir))
                {
                    effectiveWorkingDir = Environment.CurrentDirectory;
                }
            }

            if (!NativeMethods.CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, (uint)creationFlags, environmentBlock, effectiveWorkingDir, ref startupInfo, out var processInfo))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            processHandle = new SafeFileHandle(processInfo.hProcess, ownsHandle: true);
            threadHandle = new SafeFileHandle(processInfo.hThread, ownsHandle: true);
            _process = Process.GetProcessById(processInfo.dwProcessId);
        }
        finally
        {
            threadHandle?.Dispose();
            processHandle?.Dispose();
            if (attributeList != IntPtr.Zero) { NativeMethods.DeleteProcThreadAttributeList(attributeList); Marshal.FreeHGlobal(attributeList); }
            if (commandLine != IntPtr.Zero) Marshal.FreeHGlobal(commandLine);
            if (environmentBlock != IntPtr.Zero) Marshal.FreeHGlobal(environmentBlock);
        }
    }

    private string GenerateShellCommandLine(string? workingDir)
    {
        if (!string.IsNullOrWhiteSpace(_customExecutable))
        {
            var args = string.IsNullOrWhiteSpace(_customArguments) ? "" : $" {_customArguments}";
            if (_shellType == ShellType.WSL && !string.IsNullOrWhiteSpace(workingDir) && !args.Contains("--cd"))
            {
                return $"\"{_customExecutable}\" --cd \"{workingDir}\"{args}";
            }
            return $"\"{_customExecutable}\"{args}";
        }

        if (_shellType == ShellType.PowerShell)
        {
            string exePath = ResolveExecutable("pwsh.exe") ?? "powershell.exe";

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
                    $loc = $ExecutionContext.SessionState.Path.CurrentLocation.Path
                    $last = (Get-History -Count 1).CommandLine
                    if ($last -and ($last.Trim() -notmatch '^(chcp(\s|$)|\[Console\]::|\$OutputEncoding|\$function:prompt|Set-PSReadLineOption|__multishell_)')) {
                        $b = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($last))
                        [Console]::Write([char]27 + ']133;E;' + $b + [char]7)
                    }
                    [Console]::Write([char]27 + ']9;9;"' + $loc + '"' + [char]7)
                    "PS $loc$('>' * ($nestedPromptLevel + 1)) "
                }
                """;

            // Encode as UTF-16LE Base64 for -EncodedCommand; avoids all quoting issues.
            string encodedHook = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(hookScript));
            return $"\"{exePath}\" -NoLogo -NoExit -EncodedCommand {encodedHook}";
        }

        if (_shellType == ShellType.NuShell)
        {
            string exePath = ResolveExecutable("nu.exe") ?? "nu.exe";
            return $"\"{exePath}\"";
        }

        if (_shellType == ShellType.WSL)
        {
            string exePath = ResolveExecutable("wsl.exe") ?? "wsl.exe";
            if (!string.IsNullOrWhiteSpace(workingDir))
            {
                return $"\"{exePath}\" --cd \"{workingDir}\"";
            }
            return $"\"{exePath}\"";
        }

        return "cmd.exe /K \"chcp 65001 >nul\"";
    }

    private static string? ResolveExecutable(string name)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        if (name == "pwsh.exe")
        {
            var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var winPowerShell = Path.Combine(windir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(winPowerShell)) return winPowerShell;
        }
        return null;
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
