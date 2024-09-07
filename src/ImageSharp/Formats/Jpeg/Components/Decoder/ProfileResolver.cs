// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Provides methods for identifying metadata and color profiles within jpeg images.
/// </summary>
internal static class ProfileResolver
{
    /// <summary>
    /// Gets the JFIF specific markers.
    /// </summary>
    public static ReadOnlySpan<byte> JFifMarker =>
    [
        (byte)'J', (byte)'F', (byte)'I', (byte)'F', (byte)'\0'
    ];

    /// <summary>
    /// Gets the JFXX specific markers.
    /// </summary>
    public static ReadOnlySpan<byte> JFxxMarker =>
    [
        (byte)'J', (byte)'F', (byte)'X', (byte)'X', (byte)'\0'
    ];

    /// <summary>
    /// Gets the ICC specific markers.
    /// </summary>
    public static ReadOnlySpan<byte> IccMarker =>
    [
        (byte)'I', (byte)'C', (byte)'C', (byte)'_',
        (byte)'P', (byte)'R', (byte)'O', (byte)'F',
        (byte)'I', (byte)'L', (byte)'E', (byte)'\0'
    ];

    /// <summary>
    /// Gets the adobe photoshop APP13 marker which can contain IPTC meta data.
    /// </summary>
    public static ReadOnlySpan<byte> AdobePhotoshopApp13Marker =>
    [
        (byte)'P', (byte)'h', (byte)'o', (byte)'t', (byte)'o', (byte)'s', (byte)'h', (byte)'o', (byte)'p', (byte)' ', (byte)'3', (byte)'.', (byte)'0', (byte)'\0'
    ];

    /// <summary>
    /// Gets the 8BIM marker, which signals the start of a adobe specific image resource block.
    /// </summary>
    public static ReadOnlySpan<byte> AdobeImageResourceBlockMarker =>
    [
        (byte)'8', (byte)'B', (byte)'I', (byte)'M'
    ];

    /// <summary>
    /// Gets a IPTC Image resource ID.
    /// </summary>
    public static ReadOnlySpan<byte> AdobeIptcMarker =>
    [
        (byte)4, (byte)4
    ];

    /// <summary>
    /// Gets the EXIF specific markers.
    /// </summary>
    public static ReadOnlySpan<byte> ExifMarker =>
    [
        (byte)'E', (byte)'x', (byte)'i', (byte)'f', (byte)'\0', (byte)'\0'
    ];

    /// <summary>
    /// Gets the XMP specific markers.
    /// </summary>
    public static ReadOnlySpan<byte> XmpMarker =>
    [
        (byte)'h', (byte)'t', (byte)'t', (byte)'p', (byte)':', (byte)'/', (byte)'/',
        (byte)'n', (byte)'s', (byte)'.', (byte)'a', (byte)'d', (byte)'o', (byte)'b',
        (byte)'e', (byte)'.', (byte)'c', (byte)'o', (byte)'m', (byte)'/', (byte)'x',
        (byte)'a', (byte)'p', (byte)'/', (byte)'1', (byte)'.', (byte)'0', (byte)'/',
        (byte)0
    ];

    /// <summary>
    /// Gets the Adobe specific markers <see href="http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html#Adobe"/>.
    /// </summary>
    public static ReadOnlySpan<byte> AdobeMarker =>
    [
        (byte)'A', (byte)'d', (byte)'o', (byte)'b', (byte)'e'
    ];

    /// <summary>
    /// Returns a value indicating whether the passed bytes are a match to the profile identifier.
    /// </summary>
    /// <param name="bytesToCheck">The bytes to check.</param>
    /// <param name="profileIdentifier">The profile identifier.</param>
    /// <returns>The <see cref="bool"/>.</returns>
    public static bool IsProfile(ReadOnlySpan<byte> bytesToCheck, ReadOnlySpan<byte> profileIdentifier)
    {
        return bytesToCheck.Length >= profileIdentifier.Length
               && bytesToCheck[..profileIdentifier.Length].SequenceEqual(profileIdentifier);
    }
}
