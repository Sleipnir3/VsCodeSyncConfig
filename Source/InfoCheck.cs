using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

class InformationCheck
{
    /// <summary>
    /// 检查平台类型、CPU信息、VSCode安装情况
    /// </summary>
    public static bool CheckPcAndVSCodeInfo()
    {
        // 1. 平台类型
        string platform;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            platform = "Windows";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            platform = "macOS";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            platform = "Linux";
        else
            platform = "Other";

        Console.WriteLine($"Platform: {platform}");

        // 2. CPU 信息
        Console.WriteLine($"CPU Architecture: {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"Logical Processor Count: {Environment.ProcessorCount}");

        // 3. VSCode 信息
        string codeVersion = GetVSCodeVersion(platform);
        if (string.IsNullOrEmpty(codeVersion))
            Console.WriteLine("VS Code: Not installed or not found in PATH");
        else
            Console.WriteLine($"VS Code Version: {codeVersion}");
        return !string.IsNullOrEmpty(codeVersion); 
    }

    /// <summary>
    /// 获取 VSCode 版本（如果已安装）
    /// </summary>
    static string GetVSCodeVersion(string platform)
    {
        try
        {
            string shell, args;
            if (platform == "Windows")
            {
                shell = "cmd.exe";
                args = "/c code --version";
            }
            else if (platform == "macOS")
            {
                shell = "/bin/zsh"; // macOS 默认 shell
                args = "-c \"code --version\"";
            }
            else // Linux 或其他类 Unix
            {
                shell = "/bin/bash";
                args = "-c \"code --version\"";
            }

            var psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (var process = Process.Start(psi))
            {
                string output = process!.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return string.IsNullOrWhiteSpace(output) ? null : output.Replace("\n", " | ");
            }
        }
        catch
        {
            return null;
        }
    }
}
