// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

/// <summary>
/// Owns the headers shared by all frames in one codestream.
/// </summary>
internal sealed class JxlCodecMetadata
{
    /// <summary>
    /// Gets the image metadata shared by the codestream's frames.
    /// </summary>
    /// <remarks>The parser updates this instance. Frame bundles retain the same metadata throughout decoding.</remarks>
    public JxlImageMetadata ImageMetadata { get; } = new();

    /// <summary>
    /// Gets the size header that the parser fills before decoding frames.
    /// </summary>
    public JxlSizeHeader Size { get; } = new();

    /// <summary>
    /// Gets the transform data that the parser fills before decoding frames.
    /// </summary>
    public JxlCustomTransformData CustomTransformData { get; } = new();

    /// <summary>
    /// Gets the encoded image width in pixels.
    /// </summary>
    public int XSize => this.Size.XSize;

    /// <summary>
    /// Gets the encoded image height in pixels.
    /// </summary>
    public int YSize => this.Size.YSize;

    /// <summary>
    /// Gets the preview width after applying the requested orientation policy.
    /// </summary>
    /// <param name="keepOrientation">Whether to retain the encoded orientation instead of rotating the samples.</param>
    /// <returns>The preview width in pixels.</returns>
    public int GetOrientedPreviewXSize(bool keepOrientation)
    {
        if (this.ImageMetadata.Orientation > 4 && !keepOrientation)
        {
            return this.ImageMetadata.PreviewSize.YSize;
        }

        return this.ImageMetadata.PreviewSize.XSize;
    }

    /// <summary>
    /// Gets the preview height after applying the requested orientation policy.
    /// </summary>
    /// <param name="keepOrientation">Whether to retain the encoded orientation instead of rotating the samples.</param>
    /// <returns>The preview height in pixels.</returns>
    public int GetOrientedPreviewYSize(bool keepOrientation)
    {
        if (this.ImageMetadata.Orientation > 4 && !keepOrientation)
        {
            return this.ImageMetadata.PreviewSize.XSize;
        }

        return this.ImageMetadata.PreviewSize.YSize;
    }

    /// <summary>
    /// Gets the image width after applying the requested orientation policy.
    /// </summary>
    /// <param name="keepOrientation">Whether to retain the encoded orientation instead of rotating the samples.</param>
    /// <returns>The image width in pixels.</returns>
    public int GetOrientedXSize(bool keepOrientation)
    {
        if (this.ImageMetadata.Orientation > 4 && !keepOrientation)
        {
            return this.YSize;
        }

        return this.XSize;
    }

    /// <summary>
    /// Gets the image height after applying the requested orientation policy.
    /// </summary>
    /// <param name="keepOrientation">Whether to retain the encoded orientation instead of rotating the samples.</param>
    /// <returns>The image height in pixels.</returns>
    public int GetOrientedYSize(bool keepOrientation)
    {
        if (this.ImageMetadata.Orientation > 4 && !keepOrientation)
        {
            return this.XSize;
        }

        return this.YSize;
    }
}
