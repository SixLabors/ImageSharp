// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// The base class for all stateful image decoders.
/// </summary>
internal abstract class ImageDecoderCore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    protected ImageDecoderCore(DecoderOptions options)
        => this.Options = options;

    /// <summary>
    /// Gets the general decoder options.
    /// </summary>
    public DecoderOptions Options { get; }

    /// <summary>
    /// Gets or sets the dimensions of the image being decoded.
    /// </summary>
    public Size Dimensions { get; protected internal set; }

    /// <summary>
    /// Reads the raw image information from the specified stream.
    /// </summary>
    /// <param name="configuration">The shared configuration.</param>
    /// <param name="stream">The <see cref="Stream" /> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="ImageInfo" />.</returns>
    /// <exception cref="InvalidImageContentException">Thrown if the encoded image contains errors.</exception>
    public ImageInfo Identify(
        Configuration configuration,
        Stream stream,
        CancellationToken cancellationToken)
    {
        using BufferedReadStream bufferedReadStream = new(configuration, stream, cancellationToken);

        try
        {
            return this.Identify(bufferedReadStream, cancellationToken);
        }
        catch (InvalidMemoryOperationException ex)
        {
            throw new InvalidImageContentException(this.Dimensions, ex);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}" /> of a specific pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="configuration">The shared configuration.</param>
    /// <param name="stream">The <see cref="Stream" /> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="Image{TPixel}" />.</returns>
    /// <exception cref="InvalidImageContentException">Thrown if the encoded image contains errors.</exception>
    public Image<TPixel> Decode<TPixel>(
        Configuration configuration,
        Stream stream,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Test may pass a BufferedReadStream in order to monitor EOF hits, if so, use the existing instance.
        BufferedReadStream bufferedReadStream =
            stream as BufferedReadStream ?? new BufferedReadStream(configuration, stream, cancellationToken);

        try
        {
            return this.Decode<TPixel>(bufferedReadStream, cancellationToken);
        }
        catch (InvalidMemoryOperationException ex)
        {
            throw new InvalidImageContentException(this.Dimensions, ex);
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (bufferedReadStream != stream)
            {
                bufferedReadStream.Dispose();
            }
        }
    }

    /// <summary>
    /// Reads the raw image information from the specified stream.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="ImageInfo"/>.</returns>
    /// <remarks>
    /// Cancellable synchronous method. In case of cancellation,
    /// an <see cref="OperationCanceledException"/> shall be thrown which will be handled on the call site.
    /// </remarks>
    protected abstract ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken);

    /// <summary>
    /// Decodes the image from the specified stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The stream, where the image should be decoded from. Cannot be null.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <returns>The decoded image.</returns>
    /// <remarks>
    /// Cancellable synchronous method. In case of cancellation, an <see cref="OperationCanceledException"/> shall
    /// be thrown which will be handled on the call site.
    /// </remarks>
    protected abstract Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Converts the ICC color profile of the specified image to the compact sRGB v4 profile if a source profile is
    /// available.
    /// </summary>
    /// <remarks>
    /// This method should only be used by decoders that gurantee that the encoded image data is in a color space
    /// compatible with sRGB (e.g. standard RGB, Adobe RGB, ProPhoto RGB).
    /// <br/>
    /// If the image does not have a valid ICC profile for color conversion, no changes are made.
    /// This operation may affect the color appearance of the image to ensure consistency with the sRGB color
    /// space.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The image whose ICC profile will be converted to the compact sRGB v4 profile.</param>
    /// <returns>
    /// <see langword="true"/> if the conversion was performed; otherwise, <see langword="false"/>.
    /// </returns>
    protected bool TryConvertIccProfile<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!this.Options.TryGetIccProfileForColorConversion(image.Metadata.IccProfile, out IccProfile? profile))
        {
            return false;
        }

        ColorConversionOptions options = new()
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = image.Configuration.MemoryAllocator,
        };

        ColorProfileConverter converter = new(options);
        converter.Convert(image);
        return true;
    }
}
