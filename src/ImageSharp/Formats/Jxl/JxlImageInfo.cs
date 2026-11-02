// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Image information specific to the JPEG XL format.
/// </summary>
public class JxlImageInfo : ImageInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlImageInfo"/> class. 
    /// </summary>
    /// <param name="size">Image size</param>
    /// <param name="metadata">Image metadata</param>
    public JxlImageInfo(Size size, ImageMetadata metadata)
        : base(size, metadata)
    {
    }
}
