// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Abstracts enumeration of all fields into a visitor.
/// </summary>
internal interface IJxlFields
{
    /// <summary>
    /// Visits all fields into the specified JXL visitor.
    /// </summary>
    /// <param name="visitor">The visitor to use to visit all fields.</param>
    /// <returns>Status of the visit operation.</returns>
    public bool Visit(JxlVisitor visitor);
}
