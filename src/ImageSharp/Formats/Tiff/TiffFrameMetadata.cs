// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

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

    /// <inheritdoc/>
    public static TiffFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
        => new();

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
        => new();

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
    internal static void Parse(TiffFrameMetadata meta, ExifProfile profile)
    {
        if (profile != null)
        {
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

            // TODO: Why do we remove this? Encoding should overwrite.
            profile.RemoveValue(ExifTag.BitsPerSample);
            profile.RemoveValue(ExifTag.Compression);
            profile.RemoveValue(ExifTag.PhotometricInterpretation);
            profile.RemoveValue(ExifTag.Predictor);
        }
    }
}
