// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Runs an external libjxl tool (cjxl/djxl). JPEG XL encoding in this
/// format is delegated to libjxl because ImageSharp has no managed JXL
/// codec yet; only cjxl can also transcode legacy JPEG bit-exactly.
/// </summary>
internal static class JxlTool
{
    public static void Run(string tool, params string[] args)
    {
        var startInfo = new ProcessStartInfo(tool)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process? process = Process.Start(startInfo);
        if (process is null)
        {
            throw new ImageFormatException($"Failed to start '{tool}'. Install libjxl (cjxl/djxl) to use JPEG XL.");
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new ImageFormatException($"'{tool}' failed with exit code {process.ExitCode}: {process.StandardError.ReadToEnd()}");
        }
    }

    public static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "imagesharp-jxl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
