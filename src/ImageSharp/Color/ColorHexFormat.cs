// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp;

/// <summary>
/// Specifies the channel order when formatting or parsing a color as a hexadecimal string.
/// </summary>
public enum ColorHexFormat
{
    /// <summary>
    /// Uses <c>RRGGBBAA</c> channel order where the red, green, and blue components come first,
    /// followed by the alpha component. This matches the CSS Color Module Level 4 and common web standards.
    /// <para>
    /// When parsing, supports the following formats:
    /// <list type="bullet">
    /// <item><description><c>#RGB</c> expands to <c>RRGGBBFF</c> (fully opaque)</description></item>
    /// <item><description><c>#RGBA</c> expands to <c>RRGGBBAA</c></description></item>
    /// <item><description><c>#RRGGBB</c> expands to <c>RRGGBBFF</c> (fully opaque)</description></item>
    /// <item><description><c>#RRGGBBAA</c> used as-is</description></item>
    /// </list>
    /// </para>
    /// When formatting, outputs an 8-digit hex string in <c>RRGGBBAA</c> order.
    /// </summary>
    Rgba,

    /// <summary>
    /// Uses <c>AARRGGBB</c> channel order where the alpha component comes first,
    /// followed by the red, green, and blue components. This matches the Microsoft/XAML convention.
    /// <para>
    /// When parsing, supports the following formats:
    /// <list type="bullet">
    /// <item><description><c>#ARGB</c> expands to <c>AARRGGBB</c></description></item>
    /// <item><description><c>#AARRGGBB</c> used as-is</description></item>
    /// </list>
    /// </para>
    /// When formatting, outputs an 8-digit hex string in <c>AARRGGBB</c> order.
    /// </summary>
    Argb
}
