// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

internal static class JpegDebug
{
    [Conditional("DEBUG")]
    public static void LogWarning(string message) =>
        // TODO: do we allow Debug.WriteLine in this codebase?
        Debug.WriteLine($"⚠️ JPEG XL [JPEG parser]: {message}");
}
