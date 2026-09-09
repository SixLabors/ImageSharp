// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Carries the syntax and sample-storage parameters required to reconstruct one AV1 transform block.
/// </summary>
internal struct Av1TransformFunctionParameters
{
    /// <summary>
    /// Gets or sets the compound transform type.
    /// </summary>
    public Av1TransformType TransformType { get; set; }

    /// <summary>
    /// Gets or sets the transform-block dimensions.
    /// </summary>
    public Av1TransformSize TransformSize { get; set; }

    /// <summary>
    /// Gets or sets the number of coefficient positions represented by the decoded coefficient buffer.
    /// </summary>
    public int EndOfBuffer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the coded segment uses the AV1 lossless transform rules.
    /// </summary>
    public bool IsLossless { get; set; }

    /// <summary>
    /// Gets or sets the decoded sample bit depth.
    /// </summary>
    public int BitDepth { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reconstructed samples use the 16-bit storage pipeline.
    /// </summary>
    public bool Is16BitPipeline { get; set; }
}
