// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// Encodes CUR containers using CUR frame metadata.
/// </summary>
internal sealed class CurEncoderCore : IconEncoderCore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurEncoderCore"/> class.
    /// </summary>
    /// <param name="encoder">The encoder options.</param>
    public CurEncoderCore(QuantizingImageEncoder encoder)
        : base(encoder, IconFileType.CUR)
    {
    }

    /// <summary>
    /// Encodes all source frames as a CUR resource.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        => this.Encode(image, stream, default(CurFrameMetadataProvider), cancellationToken);

    /// <summary>
    /// Supplies CUR frame metadata to the shared container encoder.
    /// </summary>
    private readonly struct CurFrameMetadataProvider : IEncodingFrameMetadataProvider
    {
        /// <inheritdoc/>
        public EncodingFrameMetadata GetEncodingFrameMetadata(ImageFrame frame, out ReadOnlyMemory<Color>? colorTable)
        {
            CurFrameMetadata metadata = frame.Metadata.GetCurMetadata();
            colorTable = metadata.ColorTable;
            return new EncodingFrameMetadata(metadata.Compression, metadata.BmpBitsPerPixel, metadata.ToIconDirEntry(frame.Size));
        }
    }
}
