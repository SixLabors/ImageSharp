// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the JPEGTables exif tag.
    /// </summary>
    public static ExifTag<byte[]> JPEGTables { get; } = new(ExifTagValue.JPEGTables);

    /// <summary>
    /// Gets the OECF exif tag.
    /// </summary>
    public static ExifTag<byte[]> OECF { get; } = new(ExifTagValue.OECF);

    /// <summary>
    /// Gets the ExifVersion exif tag.
    /// </summary>
    public static ExifTag<byte[]> ExifVersion { get; } = new(ExifTagValue.ExifVersion);

    /// <summary>
    /// Gets the ComponentsConfiguration exif tag.
    /// </summary>
    public static ExifTag<byte[]> ComponentsConfiguration { get; } = new(ExifTagValue.ComponentsConfiguration);

    /// <summary>
    /// Gets the MakerNote exif tag.
    /// </summary>
    public static ExifTag<byte[]> MakerNote { get; } = new(ExifTagValue.MakerNote);

    /// <summary>
    /// Gets the FlashpixVersion exif tag.
    /// </summary>
    public static ExifTag<byte[]> FlashpixVersion { get; } = new(ExifTagValue.FlashpixVersion);

    /// <summary>
    /// Gets the SpatialFrequencyResponse exif tag.
    /// </summary>
    public static ExifTag<byte[]> SpatialFrequencyResponse { get; } = new(ExifTagValue.SpatialFrequencyResponse);

    /// <summary>
    /// Gets the SpatialFrequencyResponse2 exif tag.
    /// </summary>
    public static ExifTag<byte[]> SpatialFrequencyResponse2 { get; } = new(ExifTagValue.SpatialFrequencyResponse2);

    /// <summary>
    /// Gets the Noise exif tag.
    /// </summary>
    public static ExifTag<byte[]> Noise { get; } = new(ExifTagValue.Noise);

    /// <summary>
    /// Gets the CFAPattern exif tag.
    /// </summary>
    public static ExifTag<byte[]> CFAPattern { get; } = new(ExifTagValue.CFAPattern);

    /// <summary>
    /// Gets the DeviceSettingDescription exif tag.
    /// </summary>
    public static ExifTag<byte[]> DeviceSettingDescription { get; } = new(ExifTagValue.DeviceSettingDescription);

    /// <summary>
    /// Gets the ImageSourceData exif tag.
    /// </summary>
    public static ExifTag<byte[]> ImageSourceData { get; } = new(ExifTagValue.ImageSourceData);

    /// <summary>
    /// Gets the FileSource exif tag.
    /// </summary>
    public static ExifTag<byte> FileSource { get; } = new(ExifTagValue.FileSource);

    /// <summary>
    /// Gets the ImageDescription exif tag.
    /// </summary>
    public static ExifTag<byte> SceneType { get; } = new(ExifTagValue.SceneType);
}
