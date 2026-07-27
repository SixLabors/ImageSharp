// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ico;

/// <summary>
/// Encodes ICO containers using ICO frame metadata.
/// </summary>
internal sealed class IcoEncoderCore : IconEncoderCore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IcoEncoderCore"/> class.
    /// </summary>
    /// <param name="encoder">The encoder options.</param>
    public IcoEncoderCore(QuantizingImageEncoder encoder)
        : base(encoder, IconFileType.ICO)
    {
    }

    /// <summary>
    /// Encodes all source frames as an ICO resource.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        => this.Encode(image, stream, default(IcoFrameMetadataProvider), cancellationToken);

    /// <summary>
    /// Supplies ICO frame metadata to the shared container encoder.
    /// </summary>
    private readonly struct IcoFrameMetadataProvider : IEncodingFrameMetadataProvider
    {
        /// <inheritdoc/>
        public EncodingFrameMetadata GetEncodingFrameMetadata(ImageFrame frame, out ReadOnlyMemory<Color>? colorTable)
        {
            IcoFrameMetadata metadata = frame.Metadata.GetIcoMetadata();
            colorTable = metadata.ColorTable;
            return new EncodingFrameMetadata(metadata.Compression, metadata.BmpBitsPerPixel, metadata.ToIconDirEntry(frame.Size));
        }
    }
}
