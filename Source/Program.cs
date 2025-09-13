using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (InformationCheck.CheckPcAndVSCodeInfo())
        {
            Console.WriteLine("Select mode:");
            Console.WriteLine("1 - Collect VSCode config → Configs/<timestamp>/");
            Console.WriteLine("2 - Sync latest snapshot → VSCode user dir");
            Console.Write("Enter number and press Enter: ");

            string choice = Console.ReadLine()?.Trim();
            if (choice == "1")
            {
                CollectConfig();
            }
            else if (choice == "2")
            {
                SyncConfig();
            }
            else
            {
                Console.WriteLine("Invalid selection. Exit.");
            }
        }
        else
            Console.WriteLine("The VSCode is not exist!");
    }

    // Collects VSCode config to Configs/<timestamp> relative to the parent of the executable dir (i.e., root with Tools/ and Configs/)
    static void CollectConfig()
    {
        string vscodeUserDir = GetVSCodeUserDir();
        if (!Directory.Exists(vscodeUserDir))
        {
            Console.WriteLine("VSCode user directory not found: " + vscodeUserDir);
            return;
        }

        string projectRoot = GetProjectRoot();
        string configsRoot = Path.Combine(projectRoot, "Configs");
        Directory.CreateDirectory(configsRoot);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
        string targetDir = Path.Combine(configsRoot, timestamp);
        Directory.CreateDirectory(targetDir);

        CopyIfExists(Path.Combine(vscodeUserDir, "settings.json"), targetDir);
        CopyIfExists(Path.Combine(vscodeUserDir, "keybindings.json"), targetDir);

        string snippetsSrc = Path.Combine(vscodeUserDir, "snippets");
        string snippetsDst = Path.Combine(targetDir, "snippets");
        if (Directory.Exists(snippetsSrc))
        {
            CopyDirectory(snippetsSrc, snippetsDst);
            Console.WriteLine("Copied: snippets/");
        }

        // Export extensions
        string extensionsFile = Path.Combine(targetDir, "extensions.txt");
        try
        {
            string listCmd = GetVSCodeListExtensionsCommand();
            string output = RunCommandCaptureStdout(listCmd);
            File.WriteAllText(extensionsFile, output ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine("Exported extensions: extensions.txt");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to export extensions. Ensure VSCode CLI is available. " + ex.Message);
        }

        Console.WriteLine($"Done. Saved to: {targetDir}");
        OpenDirectory(targetDir);
    }

    // Syncs the latest snapshot from Configs/<latest> back into the VSCode user dir and installs extensions
    static void SyncConfig()
    {
        string vscodeUserDir = GetVSCodeUserDir();
        if (!Directory.Exists(vscodeUserDir))
        {
            Console.WriteLine("VSCode user directory not found: " + vscodeUserDir);
            return;
        }

        string projectRoot = GetProjectRoot();
        string configsRoot = Path.Combine(projectRoot, "Configs");
        if (!Directory.Exists(configsRoot))
        {
            Console.WriteLine("Configs directory not found: " + configsRoot);
            return;
        }

        var latestDir = Directory.GetDirectories(configsRoot)
                                 .OrderByDescending(d => d, StringComparer.Ordinal)
                                 .FirstOrDefault();
        if (latestDir == null)
        {
            Console.WriteLine("No snapshots found in Configs/");
            return;
        }

        Console.WriteLine($"Syncing: {latestDir} → {vscodeUserDir}");

        CopyIfExists(Path.Combine(latestDir, "settings.json"), vscodeUserDir);
        CopyIfExists(Path.Combine(latestDir, "keybindings.json"), vscodeUserDir);

        string snippetsSrc = Path.Combine(latestDir, "snippets");
        string snippetsDst = Path.Combine(vscodeUserDir, "snippets");
        if (Directory.Exists(snippetsSrc))
        {
            CopyDirectory(snippetsSrc, snippetsDst);
            Console.WriteLine("Synced: snippets/");
        }

        string extensionsFile = Path.Combine(latestDir, "extensions.txt");
        if (File.Exists(extensionsFile))
        {
            var extensions = File.ReadAllLines(extensionsFile, new UTF8Encoding(false));
            foreach (var ext in extensions.Select(e => e.Trim()).Where(e => !string.IsNullOrWhiteSpace(e)))
            {
                try
                {
                    string installCmd = GetVSCodeInstallExtensionCommand(ext);
                    RunCommandNoCapture(installCmd);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to install extension {ext}: {ex.Message}");
                }
            }
            Console.WriteLine("Extensions installed (forced).");
        }

        Console.WriteLine("Sync complete.");
        OpenDirectory(vscodeUserDir);
    }

    // Determine VSCode user directory per platform
    static string GetVSCodeUserDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return Path.Combine(home, "Library", "Application Support", "Code", "User");
        }
        else // Linux
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return Path.Combine(home, ".config", "Code", "User");
        }
    }

    // Get project root as parent of the executable directory (handles Tools/<exe>)
    static string GetProjectRoot()
    {
        string exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        DirectoryInfo? parent = Directory.GetParent(exeDir);
        return parent?.FullName ?? exeDir;
    }

    static void CopyIfExists(string sourceFile, string targetDir)
    {
        if (File.Exists(sourceFile))
        {
            Directory.CreateDirectory(targetDir);
            string destFile = Path.Combine(targetDir, Path.GetFileName(sourceFile));
            File.Copy(sourceFile, destFile, true);

            // Re-save as UTF-8 (no BOM), in case source encoding is non-UTF-8
            string content = File.ReadAllText(destFile, Encoding.UTF8);
            File.WriteAllText(destFile, content, new UTF8Encoding(false));

            Console.WriteLine($"Copied: {Path.GetFileName(sourceFile)}");
        }
    }

    static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);

            // Re-save as UTF-8 (no BOM) when it looks like text; otherwise just keep raw copy.
            // Simple heuristic: only enforce UTF-8 on json/code-like files.
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".json" || ext == ".code-snippets" || ext == ".txt")
            {
                try
                {
                    string content = File.ReadAllText(destFile, Encoding.UTF8);
                    File.WriteAllText(destFile, content, new UTF8Encoding(false));
                }
                catch
                {
                    // If binary or unreadable as UTF-8, keep original copy.
                }
            }
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }

    // On macOS (including M4/arm64), prefer zsh; Windows uses cmd; Linux uses bash.
    static string GetShellCommand()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "cmd.exe";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "/bin/zsh";
        return "/bin/bash"; // Linux
    }

    static string GetShellArguments(string command)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "/c " + command;
        else
            return "-c \"" + command.Replace("\"", "\\\"") + "\"";
    }

    // Try PATH "code", else macOS fallback to VS Code bundled CLI; returns a full shell command string.
    static string GetVSCodeListExtensionsCommand()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Try PATH first
            if (CommandAvailable("code"))
                return "code --list-extensions";
            // Fallback to app bundle CLI
            string fallback = "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
            if (File.Exists(fallback))
                return $"\"{fallback}\" --list-extensions";
            // If installed via user path (insiders or other path), try common Homebrew cask path
            string brewCask = "/usr/local/bin/code";
            string brewCaskArm = "/opt/homebrew/bin/code";
            if (File.Exists(brewCaskArm)) return $"\"{brewCaskArm}\" --list-extensions";
            if (File.Exists(brewCask)) return $"\"{brewCask}\" --list-extensions";
        }
        return "code --list-extensions";
    }

    static string GetVSCodeInstallExtensionCommand(string ext)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (CommandAvailable("code"))
                return $"code --install-extension {ext} --force";

            string fallback = "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
            if (File.Exists(fallback))
                return $"\"{fallback}\" --install-extension {ext} --force";

            string brewCaskArm = "/opt/homebrew/bin/code";
            string brewCask = "/usr/local/bin/code";
            if (File.Exists(brewCaskArm)) return $"\"{brewCaskArm}\" --install-extension {ext} --force";
            if (File.Exists(brewCask)) return $"\"{brewCask}\" --install-extension {ext} --force";
        }
        return $"code --install-extension {ext} --force";
    }

    static bool CommandAvailable(string cmd)
    {
        try
        {
            string shell = GetShellCommand();
            string args = GetShellArguments($"command -v {cmd} >/dev/null 2>&1 && echo OK || echo NO");
            var psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                string outp = p!.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return outp.Trim().EndsWith("OK", StringComparison.Ordinal);
            }
        }
        catch { return false; }
    }

    static string RunCommandCaptureStdout(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = GetShellCommand(),
            Arguments = GetShellArguments(command),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using (var process = Process.Start(psi))
        {
            string stdout = process!.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (!string.IsNullOrWhiteSpace(stderr))
                Console.WriteLine(stderr.Trim());
            return stdout;
        }
    }

    static void RunCommandNoCapture(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = GetShellCommand(),
            Arguments = GetShellArguments(command),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using (var process = Process.Start(psi))
        {
            process!.WaitForExit();
        }
    }

    static void OpenDirectory(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start("explorer.exe", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", path);
            else
                Process.Start("xdg-open", path);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to open directory: " + ex.Message);
        }
    }
}
