// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    /// <summary>
    /// Defines constants relating to OpenExr images.
    /// </summary>
    internal static class ExrConstants
    {
        /// <summary>
        /// The list of mimetypes that equate to a OpenExr image.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/x-exr" };

        /// <summary>
        /// The list of file extensions that equate to a OpenExr image.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "exr" };

        /// <summary>
        /// The magick bytes identifying an OpenExr image.
        /// </summary>
        public static readonly int MagickBytes = 20000630;

        /// <summary>
        /// EXR attribute names.
        /// </summary>
        internal static class AttributeNames
        {
            public const string Channels = "channels";

            public const string Compression = "compression";

            public const string DataWindow = "dataWindow";

            public const string DisplayWindow = "displayWindow";

            public const string LineOrder = "lineOrder";

            public const string PixelAspectRatio = "pixelAspectRatio";

            public const string ScreenWindowCenter = "screenWindowCenter";

            public const string ScreenWindowWidth = "screenWindowWidth";
        }

        /// <summary>
        /// EXR attribute types.
        /// </summary>
        internal static class AttibuteTypes
        {
            public const string ChannelList = "chlist";

            public const string Compression = "compression";

            public const string Float = "float";

            public const string LineOrder = "lineOrder";

            public const string TwoFloat = "v2f";

            public const string BoxInt = "box2i";
        }

        internal static class ChannelNames
        {
            public const string Red = "R";

            public const string Green = "G";

            public const string Blue = "B";

            public const string Alpha = "A";
        }
    }
}
