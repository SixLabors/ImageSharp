// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Override utilities.
/// </summary>
internal static class JxlOverrideHelpers
{
    /// <summary>
    /// Converts a boolean to an override.
    /// </summary>
    /// <param name="flag">Input boolean.</param>
    /// <returns>
    /// <see cref="JxlOverride.On"/> if true. Otherwise <see cref="JxlOverride.Off"/>.
    /// </returns>
    public static JxlOverride FromBoolean(bool flag) => flag ? JxlOverride.On : JxlOverride.Off;

    /// <summary>
    /// Converts an override to a boolean.
    /// </summary>
    /// <param name="override">The override.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <returns>
    /// If override is <see cref="JxlOverride.Default"/> returns <paramref name="defaultValue"/>.
    /// Otherwise returns true if <see cref="JxlOverride.On"/>, false if <see cref="JxlOverride.Off"/>.
    /// </returns>
    public static bool ToBoolean(JxlOverride @override, bool defaultValue)
    {
        if (@override == JxlOverride.On)
        {
            return true;
        }

        if (@override == JxlOverride.Off)
        {
            return false;
        }

        return defaultValue;
    }
}
