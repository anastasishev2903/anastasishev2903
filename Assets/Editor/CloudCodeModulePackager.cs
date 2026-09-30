#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

static class CloudCodeModulePackager
{
    public const string ModuleName = "Module";

    public static string GetProjectRoot()
    {
        return Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
    }

    public static string GetModuleCsPath()
    {
        return Path.Combine(GetProjectRoot(), "Module", "Project", "Module.cs");
    }

    public static string GetModuleCsprojPath()
    {
        return Path.Combine(GetProjectRoot(), "Module", "Project", "Module.csproj");
    }

    public static bool TryReadSourceTokens(out string appToken, out string s2sToken)
    {
        appToken = string.Empty;
        s2sToken = string.Empty;
        var path = GetModuleCsPath();
        if (!File.Exists(path))
            return false;

        var text = File.ReadAllText(path);
        appToken = ReadAssignedString(text, "APP_TOKEN");
        s2sToken = ReadAssignedString(text, "ADJUST_S2S_TOKEN");
        return true;
    }

    public static string BackupModuleSource()
    {
        var path = GetModuleCsPath();
        if (!File.Exists(path))
            throw new FileNotFoundException("Module.cs not found.", path);

        return File.ReadAllText(path);
    }

    public static void RestoreModuleSource(string backup)
    {
        if (string.IsNullOrEmpty(backup))
            return;

        File.WriteAllText(GetModuleCsPath(), backup);
    }

    public static void ApplyTokens(string appToken, string s2sToken)
    {
        var path = GetModuleCsPath();
        if (!File.Exists(path))
            throw new FileNotFoundException("Module.cs not found.", path);

        var text = File.ReadAllText(path);
        var changed = false;
        if (!string.IsNullOrWhiteSpace(appToken))
        {
            text = ReplaceAssignedString(text, "APP_TOKEN", appToken.Trim());
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(s2sToken))
        {
            text = ReplaceAssignedString(text, "ADJUST_S2S_TOKEN", s2sToken.Trim());
            changed = true;
        }

        if (changed)
            File.WriteAllText(path, text);
    }

    public static async Task PublishLinuxAsync(string outputDirectory, CancellationToken cancellationToken)
    {
        var csproj = GetModuleCsprojPath();
        if (!File.Exists(csproj))
            throw new FileNotFoundException("Module.csproj not found.", csproj);

        if (Directory.Exists(outputDirectory))
            Directory.Delete(outputDirectory, true);
        Directory.CreateDirectory(outputDirectory);

        var dotnet = FindDotnetExecutable();
        var arguments =
            "publish \"" + csproj + "\" -c Release -r linux-x64 --self-contained false " +
            "-p:PublishReadyToRun=true -o \"" + outputDirectory + "\"";

        var result = await RunProcessAsync(dotnet, arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "dotnet publish failed (" + result.ExitCode + "):\n" + result.Output);
        }

        var dllPath = Path.Combine(outputDirectory, ModuleName + ".dll");
        if (!File.Exists(dllPath))
            throw new InvalidOperationException("Publish succeeded but " + ModuleName + ".dll was not found in " + outputDirectory);
    }

    public static byte[] ZipPublishDirectory(string publishDirectory)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), "CloudCode-" + ModuleName + "-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(publishDirectory, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);
            return File.ReadAllBytes(zipPath);
        }
        finally
        {
            try
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
            catch
            {
                // Ignore temp cleanup failures.
            }
        }
    }

    static string ReadAssignedString(string source, string fieldName)
    {
        var match = FindAssignment(source, fieldName);
        if (!match.Success)
            return string.Empty;

        var valueMatch = Regex.Match(match.Value, "\"([^\"]*)\"");
        return valueMatch.Success ? valueMatch.Groups[1].Value : string.Empty;
    }

    static string ReplaceAssignedString(string source, string fieldName, string value)
    {
        var match = FindAssignment(source, fieldName);
        if (!match.Success)
            throw new InvalidOperationException("Module.cs has no " + fieldName + " assignment.");

        var escaped = (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        var replacement = Regex.Replace(match.Value, "\"[^\"]*\"", "\"" + escaped + "\"");
        return source.Substring(0, match.Index) + replacement + source.Substring(match.Index + match.Length);
    }

    static Match FindAssignment(string source, string fieldName)
    {
        var pattern = @"private\s+static\s+readonly\s+string\s+" + Regex.Escape(fieldName) + @"\s*=\s*""[^""]*""\s*;";
        return Regex.Match(source, pattern);
    }

    static string FindDotnetExecutable()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var localCandidates = new[]
        {
            Path.Combine(programFiles, "dotnet", "dotnet.exe"),
            Path.Combine(programFiles, "dotnet", "dotnet"),
            "/usr/local/share/dotnet/dotnet",
            "/usr/bin/dotnet"
        };

        for (var i = 0; i < localCandidates.Length; i++)
        {
            if (File.Exists(localCandidates[i]))
                return localCandidates[i];
        }

        return "dotnet";
    }

    static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var output = new StringBuilder();
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null)
                output.AppendLine(args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null)
                output.AppendLine(args.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException("Failed to start " + fileName);

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        while (!process.HasExited)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch
                {
                    // Ignore kill failures.
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            await Task.Delay(80, cancellationToken);
        }

        return new ProcessResult(process.ExitCode, output.ToString());
    }

    readonly struct ProcessResult
    {
        public readonly int ExitCode;
        public readonly string Output;

        public ProcessResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output ?? string.Empty;
        }
    }
}
#endif
