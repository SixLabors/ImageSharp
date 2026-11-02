// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

/// <summary>
/// Debug-related utilities for the JPEG parser.
/// </summary>
internal static class JpegDebug
{
    /// <summary>
    /// Logs a warning message, when the JPEG parser detects
    /// invalid state.
    /// </summary>
    /// <param name="message">
    /// Message to log.
    /// </param>
    /// <remarks>
    /// This function in only available in DEBUG mode.
    /// </remarks>
    [Conditional("DEBUG")]
    public static void LogWarning(string message) =>
        // TODO: do we allow Debug.WriteLine in this codebase?
        Debug.WriteLine($"⚠️ JPEG XL [JPEG parser]: {message}");
}
