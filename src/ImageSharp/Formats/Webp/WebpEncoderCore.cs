// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Image encoder for writing an image to a stream in the Webp format.
/// </summary>
internal sealed class WebpEncoderCore : IImageEncoderInternals
{
    /// <summary>
    /// Used for allocating memory during processing operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// Indicating whether the alpha plane should be compressed with Webp lossless format.
    /// Defaults to true.
    /// </summary>
    private readonly bool alphaCompression;

    /// <summary>
    /// Compression quality. Between 0 and 100.
    /// </summary>
    private readonly uint quality;

    /// <summary>
    /// Quality/speed trade-off (0=fast, 6=slower-better).
    /// </summary>
    private readonly WebpEncodingMethod method;

    /// <summary>
    /// The number of entropy-analysis passes (in [1..10]).
    /// </summary>
    private readonly int entropyPasses;

    /// <summary>
    /// Spatial Noise Shaping. 0=off, 100=maximum.
    /// </summary>
    private readonly int spatialNoiseShaping;

    /// <summary>
    /// The filter the strength of the deblocking filter, between 0 (no filtering) and 100 (maximum filtering).
    /// </summary>
    private readonly int filterStrength;

    /// <summary>
    /// Flag indicating whether to preserve the exact RGB values under transparent area. Otherwise, discard this invisible
    /// RGB information for better compression.
    /// </summary>
    private readonly WebpTransparentColorMode transparentColorMode;

    /// <summary>
    /// Whether to skip metadata during encoding.
    /// </summary>
    private readonly bool skipMetadata;

    /// <summary>
    /// Indicating whether near lossless mode should be used.
    /// </summary>
    private readonly bool nearLossless;

    /// <summary>
    /// The near lossless quality. The range is 0 (maximum preprocessing) to 100 (no preprocessing, the default).
    /// </summary>
    private readonly int nearLosslessQuality;

    /// <summary>
    /// Indicating what file format compression should be used.
    /// Defaults to lossy.
    /// </summary>
    private readonly WebpFileFormatType? fileFormat;

    /// <summary>
    /// The global configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpEncoderCore"/> class.
    /// </summary>
    /// <param name="encoder">The encoder with options.</param>
    /// <param name="configuration">The global configuration.</param>
    public WebpEncoderCore(WebpEncoder encoder, Configuration configuration)
    {
        this.configuration = configuration;
        this.memoryAllocator = configuration.MemoryAllocator;
        this.alphaCompression = encoder.UseAlphaCompression;
        this.fileFormat = encoder.FileFormat;
        this.quality = (uint)encoder.Quality;
        this.method = encoder.Method;
        this.entropyPasses = encoder.EntropyPasses;
        this.spatialNoiseShaping = encoder.SpatialNoiseShaping;
        this.filterStrength = encoder.FilterStrength;
        this.transparentColorMode = encoder.TransparentColorMode;
        this.skipMetadata = encoder.SkipMetadata;
        this.nearLossless = encoder.NearLossless;
        this.nearLosslessQuality = encoder.NearLosslessQuality;
    }

    /// <summary>
    /// Encodes the image as webp to the specified stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        bool lossless;
        if (this.fileFormat is not null)
        {
            lossless = this.fileFormat == WebpFileFormatType.Lossless;
        }
        else
        {
            WebpMetadata webpMetadata = WebpCommonUtils.GetWebpMetadata(image);
            lossless = webpMetadata.FileFormat == WebpFileFormatType.Lossless;
        }

        if (lossless)
        {
            bool hasAnimation = image.Frames.Count > 1;

            using Vp8LEncoder encoder = new(
                this.memoryAllocator,
                this.configuration,
                image.Width,
                image.Height,
                this.quality,
                this.skipMetadata,
                this.method,
                this.transparentColorMode,
                this.nearLossless,
                this.nearLosslessQuality);

            encoder.EncodeHeader(image, stream, hasAnimation);

            // Encode the first frame.
            ImageFrame<TPixel> previousFrame = image.Frames.RootFrame;
            WebpFrameMetadata frameMetadata = WebpCommonUtils.GetWebpFrameMetadata(previousFrame);
            encoder.Encode(previousFrame, previousFrame.Bounds(), frameMetadata, stream, hasAnimation);

            if (hasAnimation)
            {
                WebpDisposalMethod previousDisposal = frameMetadata.DisposalMethod;

                // Encode additional frames
                // This frame is reused to store de-duplicated pixel buffers.
                using ImageFrame<TPixel> encodingFrame = new(image.Configuration, previousFrame.Size());

                for (int i = 1; i < image.Frames.Count; i++)
                {
                    ImageFrame<TPixel> currentFrame = image.Frames[i];
                    frameMetadata = WebpCommonUtils.GetWebpFrameMetadata(currentFrame);

                    ImageFrame<TPixel>? prev = previousDisposal == WebpDisposalMethod.RestoreToBackground ? null : previousFrame;

                    (bool difference, Rectangle bounds) = AnimationUtilities.DeDuplicatePixels(image.Configuration, prev, currentFrame, encodingFrame, Vector4.Zero, ClampingMode.Even);

                    if (difference && previousDisposal != WebpDisposalMethod.RestoreToBackground)
                    {
                        if (frameMetadata.BlendMethod == WebpBlendMethod.Source)
                        {
                            // We've potentially introduced transparency within our area of interest
                            // so we need to overwrite the changed area with the full data.
                            AnimationUtilities.CopySource(currentFrame, encodingFrame, bounds);
                        }
                    }

                    using Vp8LEncoder animatedEncoder = new(
                        this.memoryAllocator,
                        this.configuration,
                        bounds.Width,
                        bounds.Height,
                        this.quality,
                        this.skipMetadata,
                        this.method,
                        this.transparentColorMode,
                        this.nearLossless,
                        this.nearLosslessQuality);

                    animatedEncoder.Encode(encodingFrame, bounds, frameMetadata, stream, hasAnimation);

                    previousFrame = currentFrame;
                    previousDisposal = frameMetadata.DisposalMethod;
                }
            }

            encoder.EncodeFooter(image, stream);
        }
        else
        {
            using Vp8Encoder encoder = new(
                this.memoryAllocator,
                this.configuration,
                image.Width,
                image.Height,
                this.quality,
                this.skipMetadata,
                this.method,
                this.entropyPasses,
                this.filterStrength,
                this.spatialNoiseShaping,
                this.alphaCompression);

            if (image.Frames.Count > 1)
            {
                // TODO: What about alpha here?
                encoder.EncodeHeader(image, stream, false, true);

                // Encode the first frame.
                ImageFrame<TPixel> previousFrame = image.Frames.RootFrame;
                WebpFrameMetadata frameMetadata = WebpCommonUtils.GetWebpFrameMetadata(previousFrame);
                WebpDisposalMethod previousDisposal = frameMetadata.DisposalMethod;

                encoder.EncodeAnimation(previousFrame, stream, previousFrame.Bounds(), frameMetadata);

                // Encode additional frames
                // This frame is reused to store de-duplicated pixel buffers.
                using ImageFrame<TPixel> encodingFrame = new(image.Configuration, previousFrame.Size());

                for (int i = 1; i < image.Frames.Count; i++)
                {
                    ImageFrame<TPixel> currentFrame = image.Frames[i];
                    frameMetadata = WebpCommonUtils.GetWebpFrameMetadata(currentFrame);

                    ImageFrame<TPixel>? prev = previousDisposal == WebpDisposalMethod.RestoreToBackground ? null : previousFrame;
                    (bool difference, Rectangle bounds) = AnimationUtilities.DeDuplicatePixels(image.Configuration, prev, currentFrame, encodingFrame, Vector4.Zero, ClampingMode.Even);

                    if (difference && previousDisposal != WebpDisposalMethod.RestoreToBackground)
                    {
                        if (frameMetadata.BlendMethod == WebpBlendMethod.Source)
                        {
                            // We've potentially introduced transparency within our area of interest
                            // so we need to overwrite the changed area with the full data.
                            AnimationUtilities.CopySource(currentFrame, encodingFrame, bounds);
                        }
                    }

                    using Vp8Encoder animatedEncoder = new(
                        this.memoryAllocator,
                        this.configuration,
                        bounds.Width,
                        bounds.Height,
                        this.quality,
                        this.skipMetadata,
                        this.method,
                        this.entropyPasses,
                        this.filterStrength,
                        this.spatialNoiseShaping,
                        this.alphaCompression);

                    animatedEncoder.EncodeAnimation(encodingFrame, stream, bounds, frameMetadata);

                    previousFrame = currentFrame;
                    previousDisposal = frameMetadata.DisposalMethod;
                }
            }
            else
            {
                encoder.EncodeStatic(stream, image);
            }

            encoder.EncodeFooter(image, stream);
        }
    }
}
