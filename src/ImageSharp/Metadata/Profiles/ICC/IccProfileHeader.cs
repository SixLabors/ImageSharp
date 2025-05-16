// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Numerics;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Contains all values of an ICC profile header.
/// </summary>
public sealed class IccProfileHeader
{
    private static readonly Vector3 TruncatedD50 = new(0.9642029F, 1F, 0.8249054F);

    // sRGB v4 Preference
    private static readonly IccProfileId StandardRgbV2 = new(0x3D0EB2DE, 0xAE9397BE, 0x9B6726CE, 0x8C0A43CE);

    // sRGB v4 Preference
    private static readonly IccProfileId StandardRgbV4 = new(0x34562ABF, 0x994CCD06, 0x6D2C5721, 0xD0D68C5D);

    // sRGB v4 Appearance
    private static readonly IccProfileId StandardRgbV4A = new(0xDF1132A1, 0x746E97B0, 0xAD85719, 0xBE711E08);

    /// <summary>
    /// Gets or sets the profile size in bytes (will be ignored when writing a profile).
    /// </summary>
    public uint Size { get; set; }

    /// <summary>
    /// Gets or sets the preferred CMM (Color Management Module) type.
    /// </summary>
    public string CmmType { get; set; }

    /// <summary>
    /// Gets or sets the profiles version number.
    /// </summary>
    public IccVersion Version { get; set; }

    /// <summary>
    /// Gets or sets the type of the profile.
    /// </summary>
    public IccProfileClass Class { get; set; }

    /// <summary>
    /// Gets or sets the data colorspace.
    /// </summary>
    public IccColorSpaceType DataColorSpace { get; set; }

    /// <summary>
    /// Gets or sets the profile connection space.
    /// </summary>
    public IccColorSpaceType ProfileConnectionSpace { get; set; }

    /// <summary>
    /// Gets or sets the date and time this profile was created.
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// Gets or sets the file signature. Should always be "acsp".
    /// Value will be ignored when writing a profile.
    /// </summary>
    public string FileSignature { get; set; }

    /// <summary>
    /// Gets or sets the primary platform this profile as created for
    /// </summary>
    public IccPrimaryPlatformType PrimaryPlatformSignature { get; set; }

    /// <summary>
    /// Gets or sets the profile flags to indicate various options for the CMM
    /// such as distributed processing and caching options.
    /// </summary>
    public IccProfileFlag Flags { get; set; }

    /// <summary>
    /// Gets or sets the device manufacturer of the device for which this profile is created.
    /// </summary>
    public uint DeviceManufacturer { get; set; }

    /// <summary>
    /// Gets or sets the model of the device for which this profile is created.
    /// </summary>
    public uint DeviceModel { get; set; }

    /// <summary>
    /// Gets or sets the device attributes unique to the particular device setup such as media type.
    /// </summary>
    public IccDeviceAttribute DeviceAttributes { get; set; }

    /// <summary>
    /// Gets or sets the rendering Intent.
    /// </summary>
    public IccRenderingIntent RenderingIntent { get; set; }

    /// <summary>
    /// Gets or sets The normalized XYZ values of the illuminant of the PCS.
    /// </summary>
    public Vector3 PcsIlluminant { get; set; }

    /// <summary>
    /// Gets or sets profile creator signature.
    /// </summary>
    public string CreatorSignature { get; set; }

    /// <summary>
    /// Gets or sets the profile ID (hash).
    /// </summary>
    public IccProfileId Id { get; set; }

    internal static bool IsLikelySrgb(IccProfileHeader header)
    {
        // Reject known perceptual-appearance profile
        // This profile employs perceptual rendering intents to maintain color appearance across different
        // devices and media, which can lead to variations from standard sRGB representations.
        if (header.Id == StandardRgbV4A)
        {
            return false;
        }

        // Accept known sRGB profile IDs
        if (header.Id == StandardRgbV2 || header.Id == StandardRgbV4)
        {
            return true;
        }

        // Fallback: best-guess heuristic
        return
            header.FileSignature == "acsp" &&
            header.DataColorSpace == IccColorSpaceType.Rgb &&
            (header.ProfileConnectionSpace == IccColorSpaceType.CieXyz || header.ProfileConnectionSpace == IccColorSpaceType.CieLab) &&
            (header.Class == IccProfileClass.DisplayDevice || header.Class == IccProfileClass.ColorSpace) &&
            header.PcsIlluminant == TruncatedD50 &&
            (header.Version.Major == 2 || header.Version.Major == 4) &&
            !string.Equals(header.CmmType, "ADBE", StringComparison.Ordinal);
    }
}
