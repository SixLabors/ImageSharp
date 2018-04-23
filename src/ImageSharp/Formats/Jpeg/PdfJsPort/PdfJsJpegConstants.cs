// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort
{
    /// <summary>
    /// Contains jpeg constant values
    /// </summary>
    internal static class PdfJsJpegConstants
    {
        /// <summary>
        /// Contains marker specific constants
        /// </summary>
        public static class Markers
        {
            /// <summary>
            /// The prefix used for all markers.
            /// </summary>
            public const byte Prefix = 0xFF;

            /// <summary>
            /// The Start of Image marker
            /// </summary>
            public const ushort SOI = 0xFFD8;

            /// <summary>
            /// The End of Image marker
            /// </summary>
            public const ushort EOI = 0xFFD9;

            /// <summary>
            /// Application specific marker for marking the jpeg format.
            /// <see href="http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html"/>
            /// </summary>
            public const ushort APP0 = 0xFFE0;

            /// <summary>
            /// Application specific marker for marking where to store metadata.
            /// </summary>
            public const ushort APP1 = 0xFFE1;

            /// <summary>
            /// Application specific marker for marking where to store ICC profile information.
            /// </summary>
            public const ushort APP2 = 0xFFE2;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const ushort APP3 = 0xFFE3;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const ushort APP4 = 0xFFE4;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const ushort APP5 = 0xFFE5;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const ushort APP6 = 0xFFE6;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const ushort APP7 = 0xFFE7;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const ushort APP8 = 0xFFE8;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const ushort APP9 = 0xFFE9;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const ushort APP10 = 0xFFEA;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const ushort APP11 = 0xFFEB;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const ushort APP12 = 0xFFEC;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const ushort APP13 = 0xFFED;

            /// <summary>
            /// Application specific marker used by Adobe for storing encoding information for DCT filters.
            /// </summary>
            public const ushort APP14 = 0xFFEE;

            /// <summary>
            /// Application specific marker used by GraphicConverter to store JPEG quality.
            /// </summary>
            public const ushort APP15 = 0xFFEF;

            /// <summary>
            /// The text comment marker
            /// </summary>
            public const ushort COM = 0xFFFE;

            /// <summary>
            /// Define Quantization Table(s) marker
            /// <remarks>
            /// Specifies one or more quantization tables.
            /// </remarks>
            /// </summary>
            public const ushort DQT = 0xFFDB;

            /// <summary>
            /// Start of Frame (baseline DCT)
            /// <remarks>
            /// Indicates that this is a baseline DCT-based JPEG, and specifies the width, height, number of components,
            /// and component subsampling (e.g., 4:2:0).
            /// </remarks>
            /// </summary>
            public const ushort SOF0 = 0xFFC0;

            /// <summary>
            /// Start Of Frame (Extended Sequential DCT)
            /// <remarks>
            /// Indicates that this is a progressive DCT-based JPEG, and specifies the width, height, number of components,
            /// and component subsampling (e.g., 4:2:0).
            /// </remarks>
            /// </summary>
            public const ushort SOF1 = 0xFFC1;

            /// <summary>
            /// Start Of Frame (progressive DCT)
            /// <remarks>
            /// Indicates that this is a progressive DCT-based JPEG, and specifies the width, height, number of components,
            /// and component subsampling (e.g., 4:2:0).
            /// </remarks>
            /// </summary>
            public const ushort SOF2 = 0xFFC2;

            /// <summary>
            /// Define Huffman Table(s)
            /// <remarks>
            /// Specifies one or more Huffman tables.
            /// </remarks>
            /// </summary>
            public const ushort DHT = 0xFFC4;

            /// <summary>
            /// Define Restart Interval
            /// <remarks>
            /// Specifies the interval between RSTn markers, in macroblocks.This marker is followed by two bytes indicating the fixed size so it can be treated like any other variable size segment.
            /// </remarks>
            /// </summary>
            public const ushort DRI = 0xFFDD;

            /// <summary>
            /// Start of Scan
            /// <remarks>
            /// Begins a top-to-bottom scan of the image. In baseline DCT JPEG images, there is generally a single scan.
            /// Progressive DCT JPEG images usually contain multiple scans. This marker specifies which slice of data it
            /// will contain, and is immediately followed by entropy-coded data.
            /// </remarks>
            /// </summary>
            public const ushort SOS = 0xFFDA;

            /// <summary>
            /// Define First Restart
            /// <remarks>
            /// Inserted every r macroblocks, where r is the restart interval set by a DRI marker.
            /// Not used if there was no DRI marker. The low three bits of the marker code cycle in value from 0 to 7.
            /// </remarks>
            /// </summary>
            public const ushort RST0 = 0xFFD0;

            /// <summary>
            /// Define Eigth Restart
            /// <remarks>
            /// Inserted every r macroblocks, where r is the restart interval set by a DRI marker.
            /// Not used if there was no DRI marker. The low three bits of the marker code cycle in value from 0 to 7.
            /// </remarks>
            /// </summary>
            public const ushort RST7 = 0xFFD7;

            /// <summary>
            /// Contains Adobe specific markers
            /// </summary>
            public static class Adobe
            {
                /// <summary>
                /// The color transform is unknown.(RGB or CMYK)
                /// </summary>
                public const byte ColorTransformUnknown = 0;

                /// <summary>
                /// The color transform is YCbCr (luminance, red chroma, blue chroma)
                /// </summary>
                public const byte ColorTransformYCbCr = 1;

                /// <summary>
                /// The color transform is YCCK (luminance, red chroma, blue chroma, keyline)
                /// </summary>
                public const byte ColorTransformYcck = 2;
            }
        }
    }
}