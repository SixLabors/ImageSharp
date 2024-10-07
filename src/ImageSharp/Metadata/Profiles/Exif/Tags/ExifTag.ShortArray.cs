// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the BitsPerSample exif tag.
    /// </summary>
    public static ExifTag<ushort[]> BitsPerSample { get; } = new(ExifTagValue.BitsPerSample);

    /// <summary>
    /// Gets the MinSampleValue exif tag.
    /// </summary>
    public static ExifTag<ushort[]> MinSampleValue { get; } = new(ExifTagValue.MinSampleValue);

    /// <summary>
    /// Gets the MaxSampleValue exif tag.
    /// </summary>
    public static ExifTag<ushort[]> MaxSampleValue { get; } = new(ExifTagValue.MaxSampleValue);

    /// <summary>
    /// Gets the GrayResponseCurve exif tag.
    /// </summary>
    public static ExifTag<ushort[]> GrayResponseCurve { get; } = new(ExifTagValue.GrayResponseCurve);

    /// <summary>
    /// Gets the ColorMap exif tag.
    /// </summary>
    public static ExifTag<ushort[]> ColorMap { get; } = new(ExifTagValue.ColorMap);

    /// <summary>
    /// Gets the ExtraSamples exif tag.
    /// </summary>
    public static ExifTag<ushort[]> ExtraSamples { get; } = new(ExifTagValue.ExtraSamples);

    /// <summary>
    /// Gets the PageNumber exif tag.
    /// </summary>
    public static ExifTag<ushort[]> PageNumber { get; } = new(ExifTagValue.PageNumber);

    /// <summary>
    /// Gets the TransferFunction exif tag.
    /// </summary>
    public static ExifTag<ushort[]> TransferFunction { get; } = new(ExifTagValue.TransferFunction);

    /// <summary>
    /// Gets the HalftoneHints exif tag.
    /// </summary>
    public static ExifTag<ushort[]> HalftoneHints { get; } = new(ExifTagValue.HalftoneHints);

    /// <summary>
    /// Gets the SampleFormat exif tag.
    /// </summary>
    public static ExifTag<ushort[]> SampleFormat { get; } = new(ExifTagValue.SampleFormat);

    /// <summary>
    /// Gets the TransferRange exif tag.
    /// </summary>
    public static ExifTag<ushort[]> TransferRange { get; } = new(ExifTagValue.TransferRange);

    /// <summary>
    /// Gets the DefaultImageColor exif tag.
    /// </summary>
    public static ExifTag<ushort[]> DefaultImageColor { get; } = new(ExifTagValue.DefaultImageColor);

    /// <summary>
    /// Gets the JPEGLosslessPredictors exif tag.
    /// </summary>
    public static ExifTag<ushort[]> JPEGLosslessPredictors { get; } = new(ExifTagValue.JPEGLosslessPredictors);

    /// <summary>
    /// Gets the JPEGPointTransforms exif tag.
    /// </summary>
    public static ExifTag<ushort[]> JPEGPointTransforms { get; } = new(ExifTagValue.JPEGPointTransforms);

    /// <summary>
    /// Gets the YCbCrSubsampling exif tag.
    /// </summary>
    public static ExifTag<ushort[]> YCbCrSubsampling { get; } = new(ExifTagValue.YCbCrSubsampling);

    /// <summary>
    /// Gets the CFARepeatPatternDim exif tag.
    /// </summary>
    public static ExifTag<ushort[]> CFARepeatPatternDim { get; } = new(ExifTagValue.CFARepeatPatternDim);

    /// <summary>
    /// Gets the IntergraphPacketData exif tag.
    /// </summary>
    public static ExifTag<ushort[]> IntergraphPacketData { get; } = new(ExifTagValue.IntergraphPacketData);

    /// <summary>
    /// Gets the ISOSpeedRatings exif tag.
    /// </summary>
    public static ExifTag<ushort[]> ISOSpeedRatings { get; } = new(ExifTagValue.ISOSpeedRatings);

    /// <summary>
    /// Gets the SubjectArea exif tag.
    /// </summary>
    public static ExifTag<ushort[]> SubjectArea { get; } = new(ExifTagValue.SubjectArea);

    /// <summary>
    /// Gets the SubjectLocation exif tag.
    /// </summary>
    public static ExifTag<ushort[]> SubjectLocation { get; } = new(ExifTagValue.SubjectLocation);
}
