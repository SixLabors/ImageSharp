// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the OldSubfileType exif tag.
    /// </summary>
    public static ExifTag<ushort> OldSubfileType { get; } = new(ExifTagValue.OldSubfileType);

    /// <summary>
    /// Gets the Compression exif tag.
    /// </summary>
    public static ExifTag<ushort> Compression { get; } = new(ExifTagValue.Compression);

    /// <summary>
    /// Gets the PhotometricInterpretation exif tag.
    /// </summary>
    public static ExifTag<ushort> PhotometricInterpretation { get; } = new(ExifTagValue.PhotometricInterpretation);

    /// <summary>
    /// Gets the Thresholding exif tag.
    /// </summary>
    public static ExifTag<ushort> Thresholding { get; } = new(ExifTagValue.Thresholding);

    /// <summary>
    /// Gets the CellWidth exif tag.
    /// </summary>
    public static ExifTag<ushort> CellWidth { get; } = new(ExifTagValue.CellWidth);

    /// <summary>
    /// Gets the CellLength exif tag.
    /// </summary>
    public static ExifTag<ushort> CellLength { get; } = new(ExifTagValue.CellLength);

    /// <summary>
    /// Gets the FillOrder exif tag.
    /// </summary>
    public static ExifTag<ushort> FillOrder { get; } = new(ExifTagValue.FillOrder);

    /// <summary>
    /// Gets the Orientation exif tag.
    /// </summary>
    public static ExifTag<ushort> Orientation { get; } = new(ExifTagValue.Orientation);

    /// <summary>
    /// Gets the SamplesPerPixel exif tag.
    /// </summary>
    public static ExifTag<ushort> SamplesPerPixel { get; } = new(ExifTagValue.SamplesPerPixel);

    /// <summary>
    /// Gets the PlanarConfiguration exif tag.
    /// </summary>
    public static ExifTag<ushort> PlanarConfiguration { get; } = new(ExifTagValue.PlanarConfiguration);

    /// <summary>
    /// Gets the Predictor exif tag.
    /// </summary>
    public static ExifTag<ushort> Predictor { get; } = new(ExifTagValue.Predictor);

    /// <summary>
    /// Gets the GrayResponseUnit exif tag.
    /// </summary>
    public static ExifTag<ushort> GrayResponseUnit { get; } = new(ExifTagValue.GrayResponseUnit);

    /// <summary>
    /// Gets the ResolutionUnit exif tag.
    /// </summary>
    public static ExifTag<ushort> ResolutionUnit { get; } = new(ExifTagValue.ResolutionUnit);

    /// <summary>
    /// Gets the CleanFaxData exif tag.
    /// </summary>
    public static ExifTag<ushort> CleanFaxData { get; } = new(ExifTagValue.CleanFaxData);

    /// <summary>
    /// Gets the InkSet exif tag.
    /// </summary>
    public static ExifTag<ushort> InkSet { get; } = new(ExifTagValue.InkSet);

    /// <summary>
    /// Gets the NumberOfInks exif tag.
    /// </summary>
    public static ExifTag<ushort> NumberOfInks { get; } = new(ExifTagValue.NumberOfInks);

    /// <summary>
    /// Gets the DotRange exif tag.
    /// </summary>
    public static ExifTag<ushort> DotRange { get; } = new(ExifTagValue.DotRange);

    /// <summary>
    /// Gets the Indexed exif tag.
    /// </summary>
    public static ExifTag<ushort> Indexed { get; } = new(ExifTagValue.Indexed);

    /// <summary>
    /// Gets the OPIProxy exif tag.
    /// </summary>
    public static ExifTag<ushort> OPIProxy { get; } = new(ExifTagValue.OPIProxy);

    /// <summary>
    /// Gets the JPEGProc exif tag.
    /// </summary>
    public static ExifTag<ushort> JPEGProc { get; } = new(ExifTagValue.JPEGProc);

    /// <summary>
    /// Gets the JPEGRestartInterval exif tag.
    /// </summary>
    public static ExifTag<ushort> JPEGRestartInterval { get; } = new(ExifTagValue.JPEGRestartInterval);

    /// <summary>
    /// Gets the YCbCrPositioning exif tag.
    /// </summary>
    public static ExifTag<ushort> YCbCrPositioning { get; } = new(ExifTagValue.YCbCrPositioning);

    /// <summary>
    /// Gets the Rating exif tag.
    /// </summary>
    public static ExifTag<ushort> Rating { get; } = new(ExifTagValue.Rating);

    /// <summary>
    /// Gets the RatingPercent exif tag.
    /// </summary>
    public static ExifTag<ushort> RatingPercent { get; } = new(ExifTagValue.RatingPercent);

    /// <summary>
    /// Gets the ExposureProgram exif tag.
    /// </summary>
    public static ExifTag<ushort> ExposureProgram { get; } = new(ExifTagValue.ExposureProgram);

    /// <summary>
    /// Gets the Interlace exif tag.
    /// </summary>
    public static ExifTag<ushort> Interlace { get; } = new(ExifTagValue.Interlace);

    /// <summary>
    /// Gets the SelfTimerMode exif tag.
    /// </summary>
    public static ExifTag<ushort> SelfTimerMode { get; } = new(ExifTagValue.SelfTimerMode);

    /// <summary>
    /// Gets the SensitivityType exif tag.
    /// </summary>
    public static ExifTag<ushort> SensitivityType { get; } = new(ExifTagValue.SensitivityType);

    /// <summary>
    /// Gets the MeteringMode exif tag.
    /// </summary>
    public static ExifTag<ushort> MeteringMode { get; } = new(ExifTagValue.MeteringMode);

    /// <summary>
    /// Gets the LightSource exif tag.
    /// </summary>
    public static ExifTag<ushort> LightSource { get; } = new(ExifTagValue.LightSource);

    /// <summary>
    /// Gets the FocalPlaneResolutionUnit2 exif tag.
    /// </summary>
    public static ExifTag<ushort> FocalPlaneResolutionUnit2 { get; } = new(ExifTagValue.FocalPlaneResolutionUnit2);

    /// <summary>
    /// Gets the SensingMethod2 exif tag.
    /// </summary>
    public static ExifTag<ushort> SensingMethod2 { get; } = new(ExifTagValue.SensingMethod2);

    /// <summary>
    /// Gets the Flash exif tag.
    /// </summary>
    public static ExifTag<ushort> Flash { get; } = new(ExifTagValue.Flash);

    /// <summary>
    /// Gets the ColorSpace exif tag.
    /// </summary>
    public static ExifTag<ushort> ColorSpace { get; } = new(ExifTagValue.ColorSpace);

    /// <summary>
    /// Gets the FocalPlaneResolutionUnit exif tag.
    /// </summary>
    public static ExifTag<ushort> FocalPlaneResolutionUnit { get; } = new(ExifTagValue.FocalPlaneResolutionUnit);

    /// <summary>
    /// Gets the SensingMethod exif tag.
    /// </summary>
    public static ExifTag<ushort> SensingMethod { get; } = new(ExifTagValue.SensingMethod);

    /// <summary>
    /// Gets the CustomRendered exif tag.
    /// </summary>
    public static ExifTag<ushort> CustomRendered { get; } = new(ExifTagValue.CustomRendered);

    /// <summary>
    /// Gets the ExposureMode exif tag.
    /// </summary>
    public static ExifTag<ushort> ExposureMode { get; } = new(ExifTagValue.ExposureMode);

    /// <summary>
    /// Gets the WhiteBalance exif tag.
    /// </summary>
    public static ExifTag<ushort> WhiteBalance { get; } = new(ExifTagValue.WhiteBalance);

    /// <summary>
    /// Gets the FocalLengthIn35mmFilm exif tag.
    /// </summary>
    public static ExifTag<ushort> FocalLengthIn35mmFilm { get; } = new(ExifTagValue.FocalLengthIn35mmFilm);

    /// <summary>
    /// Gets the SceneCaptureType exif tag.
    /// </summary>
    public static ExifTag<ushort> SceneCaptureType { get; } = new(ExifTagValue.SceneCaptureType);

    /// <summary>
    /// Gets the GainControl exif tag.
    /// </summary>
    public static ExifTag<ushort> GainControl { get; } = new(ExifTagValue.GainControl);

    /// <summary>
    /// Gets the Contrast exif tag.
    /// </summary>
    public static ExifTag<ushort> Contrast { get; } = new(ExifTagValue.Contrast);

    /// <summary>
    /// Gets the Saturation exif tag.
    /// </summary>
    public static ExifTag<ushort> Saturation { get; } = new(ExifTagValue.Saturation);

    /// <summary>
    /// Gets the Sharpness exif tag.
    /// </summary>
    public static ExifTag<ushort> Sharpness { get; } = new(ExifTagValue.Sharpness);

    /// <summary>
    /// Gets the SubjectDistanceRange exif tag.
    /// </summary>
    public static ExifTag<ushort> SubjectDistanceRange { get; } = new(ExifTagValue.SubjectDistanceRange);

    /// <summary>
    /// Gets the GPSDifferential exif tag.
    /// </summary>
    public static ExifTag<ushort> GPSDifferential { get; } = new(ExifTagValue.GPSDifferential);
}
