using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        // 1. 获取 VSCode 用户配置目录
        string vscodeUserDir = GetVSCodeUserDir();
        if (!Directory.Exists(vscodeUserDir))
        {
            Console.WriteLine("未找到 VSCode 用户配置目录: " + vscodeUserDir);
            return;
        }

        // 2. 生成 Configs/时间戳 目录
        string configsRoot = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, "Configs");
        Directory.CreateDirectory(configsRoot);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmm");
        string targetDir = Path.Combine(configsRoot, timestamp);
        Directory.CreateDirectory(targetDir);

        // 3. 复制核心配置文件
        CopyIfExists(Path.Combine(vscodeUserDir, "settings.json"), targetDir);
        CopyIfExists(Path.Combine(vscodeUserDir, "keybindings.json"), targetDir);

        // 4. 复制 snippets 文件夹
        string snippetsSrc = Path.Combine(vscodeUserDir, "snippets");
        string snippetsDst = Path.Combine(targetDir, "snippets");
        if (Directory.Exists(snippetsSrc))
        {
            CopyDirectory(snippetsSrc, snippetsDst);
            Console.WriteLine("已复制 snippets/");
        }

        // 5. 导出扩展列表
        string extensionsFile = Path.Combine(targetDir, "extensions.txt");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = GetShellCommand(),
                Arguments = GetShellArguments("code --list-extensions"),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                File.WriteAllText(extensionsFile, output, Encoding.UTF8);
            }
            Console.WriteLine("已导出扩展列表: extensions.txt");
        }
        catch (Exception ex)
        {
            Console.WriteLine("无法导出扩展列表，请确认 VSCode CLI 已配置到 PATH: " + ex.Message);
        }

        Console.WriteLine($"VSCode 配置收集完成，已保存到: {targetDir}");

        // 6. 自动打开目录
        OpenDirectory(targetDir);
    }

    static string GetVSCodeUserDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support", "Code", "User");
        }
        else // Linux
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config", "Code", "User");
        }
    }

    static void CopyIfExists(string sourceFile, string targetDir)
    {
        if (File.Exists(sourceFile))
        {
            string destFile = Path.Combine(targetDir, Path.GetFileName(sourceFile));
            File.Copy(sourceFile, destFile, true);
            string content = File.ReadAllText(destFile, Encoding.UTF8);
            File.WriteAllText(destFile, content, Encoding.UTF8);
            Console.WriteLine($"已复制 {Path.GetFileName(sourceFile)}");
        }
    }

    static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
            string content = File.ReadAllText(destFile, Encoding.UTF8);
            File.WriteAllText(destFile, content, Encoding.UTF8);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }

    static string GetShellCommand()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "cmd.exe";
        else
            return "/bin/bash";
    }

    static string GetShellArguments(string command)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "/c " + command;
        else
            return "-c \"" + command + "\"";
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
            Console.WriteLine("无法打开目录: " + ex.Message);
        }
    }
}
