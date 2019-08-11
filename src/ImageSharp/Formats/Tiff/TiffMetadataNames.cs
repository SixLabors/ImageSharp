// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Defines constants for each of the supported TIFF metadata types.
    /// </summary>
    public static class TiffMetadataNames
    {
        /// <summary>
        /// Person who created the image.
        /// </summary>
        public const string Artist = "Artist";

        /// <summary>
        /// Copyright notice.
        /// </summary>
        public const string Copyright = "Copyright";

        /// <summary>
        /// Date and time of image creation.
        /// </summary>
        public const string DateTime = "DateTime";

        /// <summary>
        /// The computer and/or operating system in use at the time of image creation.
        /// </summary>
        public const string HostComputer = "HostComputer";

        /// <summary>
        /// A string that describes the subject of the image.
        /// </summary>
        public const string ImageDescription = "ImageDescription";

        /// <summary>
        /// The scanner/camera manufacturer.
        /// </summary>
        public const string Make = "Make";

        /// <summary>
        /// The scanner/camera model name or number.
        /// </summary>
        public const string Model = "Model";

        /// <summary>
        /// Name and version number of the software package(s) used to create the image.
        /// </summary>
        public const string Software = "Software";
    }
}
