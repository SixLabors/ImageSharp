// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Provides Tiff specific metadata information for the frame.
/// </summary>
public class TiffFrameMetadata : IFormatFrameMetadata<TiffFrameMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class.
    /// </summary>
    public TiffFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffFrameMetadata"/> class.
    /// </summary>
    /// <param name="other">The other tiff frame metadata.</param>
    private TiffFrameMetadata(TiffFrameMetadata other)
    {
        this.BitsPerPixel = other.BitsPerPixel;
        this.Compression = other.Compression;
        this.PhotometricInterpretation = other.PhotometricInterpretation;
        this.Predictor = other.Predictor;
        this.InkSet = other.InkSet;
        this.EncodingWidth = other.EncodingWidth;
        this.EncodingHeight = other.EncodingHeight;
    }

    /// <summary>
    /// Gets or sets the bits per pixel.
    /// </summary>
    public TiffBitsPerPixel BitsPerPixel { get; set; } = TiffConstants.DefaultBitsPerPixel;

    /// <summary>
    /// Gets or sets number of bits per component.
    /// </summary>
    public TiffBitsPerSample BitsPerSample { get; set; } = TiffConstants.DefaultBitsPerSample;

    /// <summary>
    /// Gets or sets the compression scheme used on the image data.
    /// </summary>
    public TiffCompression Compression { get; set; } = TiffConstants.DefaultCompression;

    /// <summary>
    /// Gets or sets the color space of the image data.
    /// </summary>
    public TiffPhotometricInterpretation PhotometricInterpretation { get; set; } = TiffConstants.DefaultPhotometricInterpretation;

    /// <summary>
    /// Gets or sets a mathematical operator that is applied to the image data before an encoding scheme is applied.
    /// </summary>
    public TiffPredictor Predictor { get; set; } = TiffConstants.DefaultPredictor;

    /// <summary>
    /// Gets or sets the set of inks used in a separated (<see cref="TiffPhotometricInterpretation.Separated"/>) image.
    /// </summary>
    public TiffInkSet? InkSet { get; set; }

    /// <summary>
    /// Gets or sets the encoding width.
    /// </summary>
    public int EncodingWidth { get; set; }

    /// <summary>
    /// Gets or sets the encoding height.
    /// </summary>
    public int EncodingHeight { get; set; }

    /// <inheritdoc/>
    public static TiffFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
    {
        TiffFrameMetadata frameMetadata = new();
        if (metadata.EncodingWidth.HasValue && metadata.EncodingHeight.HasValue)
        {
            frameMetadata.EncodingWidth = metadata.EncodingWidth.Value;
            frameMetadata.EncodingHeight = metadata.EncodingHeight.Value;
        }

        return frameMetadata;
    }

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
        => new()
        {
            EncodingWidth = this.EncodingWidth,
            EncodingHeight = this.EncodingHeight
        };

    /// <inheritdoc/>
    public void AfterFrameApply<TPixel>(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        float ratioX = destination.Width / (float)source.Width;
        float ratioY = destination.Height / (float)source.Height;
        this.EncodingWidth = Scale(this.EncodingWidth, destination.Width, ratioX);
        this.EncodingHeight = Scale(this.EncodingHeight, destination.Height, ratioY);

        // Overwrite the EXIF dimensional metadata with the encoding dimensions of the image.
        destination.Metadata.ExifProfile?.SyncDimensions(this.EncodingWidth, this.EncodingHeight);
    }

    private static int Scale(int value, int destination, float ratio)
    {
        if (value <= 0)
        {
            return destination;
        }

        return Math.Min((int)MathF.Ceiling(value * ratio), destination);
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public TiffFrameMetadata DeepClone() => new(this);

    /// <summary>
    /// Returns a new <see cref="TiffFrameMetadata"/> instance parsed from the given Exif profile.
    /// </summary>
    /// <param name="profile">The Exif profile containing tiff frame directory tags to parse.
    /// If null, a new instance is created and parsed instead.</param>
    /// <returns>The <see cref="TiffFrameMetadata"/>.</returns>
    internal static TiffFrameMetadata Parse(ExifProfile profile)
    {
        TiffFrameMetadata meta = new();
        Parse(meta, profile);
        return meta;
    }

    /// <summary>
    /// Parses the given Exif profile to populate the properties of the tiff frame meta data.
    /// </summary>
    /// <param name="meta">The tiff frame meta data.</param>
    /// <param name="profile">The Exif profile containing tiff frame directory tags.</param>
    private static void Parse(TiffFrameMetadata meta, ExifProfile profile)
    {
        meta.EncodingWidth = GetImageWidth(profile);
        meta.EncodingHeight = GetImageHeight(profile);

        if (profile.TryGetValue(ExifTag.BitsPerSample, out IExifValue<ushort[]>? bitsPerSampleValue)
            && TiffBitsPerSample.TryParse(bitsPerSampleValue.Value, out TiffBitsPerSample bitsPerSample))
        {
            meta.BitsPerSample = bitsPerSample;
        }

        meta.BitsPerPixel = meta.BitsPerSample.BitsPerPixel();

        if (profile.TryGetValue(ExifTag.Compression, out IExifValue<ushort>? compressionValue))
        {
            meta.Compression = (TiffCompression)compressionValue.Value;
        }

        if (profile.TryGetValue(ExifTag.PhotometricInterpretation, out IExifValue<ushort>? photometricInterpretationValue))
        {
            meta.PhotometricInterpretation = (TiffPhotometricInterpretation)photometricInterpretationValue.Value;
        }

        if (profile.TryGetValue(ExifTag.Predictor, out IExifValue<ushort>? predictorValue))
        {
            meta.Predictor = (TiffPredictor)predictorValue.Value;
        }

        if (profile.TryGetValue(ExifTag.InkSet, out IExifValue<ushort>? inkSetValue))
        {
            meta.InkSet = (TiffInkSet)inkSetValue.Value;
        }

        // Remove values, we've explicitly captured them and they could change on encode.
        profile.RemoveValue(ExifTag.BitsPerSample);
        profile.RemoveValue(ExifTag.Compression);
        profile.RemoveValue(ExifTag.PhotometricInterpretation);
        profile.RemoveValue(ExifTag.Predictor);
    }

    /// <summary>
    /// Gets the width of the image frame.
    /// </summary>
    /// <param name="exifProfile">The image frame exif profile.</param>
    /// <returns>The image width.</returns>
    private static int GetImageWidth(ExifProfile exifProfile)
    {
        if (!exifProfile.TryGetValue(ExifTag.ImageWidth, out IExifValue<Number>? width))
        {
            TiffThrowHelper.ThrowInvalidImageContentException("The TIFF image frame is missing the ImageWidth");
        }

        DebugGuard.MustBeLessThanOrEqualTo((ulong)width.Value, (ulong)int.MaxValue, nameof(ExifTag.ImageWidth));

        return (int)width.Value;
    }

    /// <summary>
    /// Gets the height of the image frame.
    /// </summary>
    /// <param name="exifProfile">The image frame exif profile.</param>
    /// <returns>The image height.</returns>
    private static int GetImageHeight(ExifProfile exifProfile)
    {
        if (!exifProfile.TryGetValue(ExifTag.ImageLength, out IExifValue<Number>? height))
        {
            TiffThrowHelper.ThrowImageFormatException("The TIFF image frame is missing the ImageLength");
        }

        return (int)height.Value;
    }
}
