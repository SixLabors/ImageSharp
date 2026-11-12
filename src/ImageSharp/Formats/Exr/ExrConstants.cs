// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Defines the OpenEXR signature, attribute identifiers and channel names.
/// </summary>
internal static class ExrConstants
{
    /// <summary>
    /// The MIME types that identify an OpenEXR image.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = new[] { "image/x-exr" };

    /// <summary>
    /// The file extensions used for OpenEXR images.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = new[] { "exr" };

    /// <summary>
    /// The integer value of the four-byte OpenEXR signature.
    /// </summary>
    public static readonly int MagickBytes = 20000630;

    /// <summary>
    /// Defines the names of OpenEXR header attributes.
    /// </summary>
    internal static class AttributeNames
    {
        /// <summary>
        /// The attribute that lists the channels and their sample layouts.
        /// </summary>
        public const string Channels = "channels";

        /// <summary>
        /// The attribute that stores the RGB primaries and white point as CIE xy coordinates.
        /// </summary>
        public const string Chromaticities = "chromaticities";

        /// <summary>
        /// The attribute that identifies the pixel-data compression method.
        /// </summary>
        public const string Compression = "compression";

        /// <summary>
        /// The attribute that gives the pixel-coordinate bounds of the stored image data.
        /// </summary>
        public const string DataWindow = "dataWindow";

        /// <summary>
        /// The attribute that gives the pixel-coordinate bounds of the intended display area.
        /// </summary>
        public const string DisplayWindow = "displayWindow";

        /// <summary>
        /// The attribute that identifies the stored scanline or tile order.
        /// </summary>
        public const string LineOrder = "lineOrder";

        /// <summary>
        /// The attribute that gives the ratio of pixel width to pixel height.
        /// </summary>
        public const string PixelAspectRatio = "pixelAspectRatio";

        /// <summary>
        /// The attribute that gives the center of the screen window.
        /// </summary>
        public const string ScreenWindowCenter = "screenWindowCenter";

        /// <summary>
        /// The attribute that gives the width of the screen window.
        /// </summary>
        public const string ScreenWindowWidth = "screenWindowWidth";

        /// <summary>
        /// The attribute that describes tile dimensions and resolution levels.
        /// </summary>
        public const string Tiles = "tiles";

        /// <summary>
        /// The attribute that gives the number of pixel-data chunks in an image part.
        /// </summary>
        public const string ChunkCount = "chunkCount";
    }

    /// <summary>
    /// Defines the type identifiers for OpenEXR header attributes.
    /// </summary>
    internal static class AttibuteTypes
    {
        /// <summary>
        /// A list of channel names, sample types and sampling factors.
        /// </summary>
        public const string ChannelList = "chlist";

        /// <summary>
        /// Eight float values in red xy, green xy, blue xy and white xy order.
        /// </summary>
        public const string Chromaticities = "chromaticities";

        /// <summary>
        /// A one-byte pixel-data compression identifier.
        /// </summary>
        public const string Compression = "compression";

        /// <summary>
        /// One little-endian 32-bit floating-point value.
        /// </summary>
        public const string Float = "float";

        /// <summary>
        /// A one-byte scanline or tile order identifier.
        /// </summary>
        public const string LineOrder = "lineOrder";

        /// <summary>
        /// Two 32-bit floating-point coordinates in x, y order.
        /// </summary>
        public const string TwoFloat = "v2f";

        /// <summary>
        /// Four signed 32-bit coordinates in minimum x, minimum y, maximum x, maximum y order.
        /// </summary>
        public const string BoxInt = "box2i";
    }

    /// <summary>
    /// Defines the standard RGB, alpha and luminance channel names.
    /// </summary>
    internal static class ChannelNames
    {
        /// <summary>
        /// The red color channel.
        /// </summary>
        public const string Red = "R";

        /// <summary>
        /// The green color channel.
        /// </summary>
        public const string Green = "G";

        /// <summary>
        /// The blue color channel.
        /// </summary>
        public const string Blue = "B";

        /// <summary>
        /// The opacity channel associated with the color channels.
        /// </summary>
        public const string Alpha = "A";

        /// <summary>
        /// The luminance channel.
        /// </summary>
        public const string Luminance = "Y";
    }
}
