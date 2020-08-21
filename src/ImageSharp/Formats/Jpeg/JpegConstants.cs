// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Contains jpeg constant values defined in the specification.
    /// </summary>
    internal static class JpegConstants
    {
        /// <summary>
        /// The maximum allowable length in each dimension of a jpeg image.
        /// </summary>
        public const ushort MaxLength = 65535;

        /// <summary>
        /// The list of mimetypes that equate to a jpeg.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/jpeg", "image/pjpeg" };

        /// <summary>
        /// The list of file extensions that equate to a jpeg.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "jpg", "jpeg", "jfif" };

        /// <summary>
        /// Contains marker specific constants.
        /// </summary>
        // ReSharper disable InconsistentNaming
        internal static class Markers
        {
            /// <summary>
            /// The prefix used for all markers.
            /// </summary>
            public const byte XFF = 0xFF;

            /// <summary>
            /// Same as <see cref="XFF"/> but of type <see cref="int"/>
            /// </summary>
            public const int XFFInt = XFF;

            /// <summary>
            /// The Start of Image marker
            /// </summary>
            public const byte SOI = 0xD8;

            /// <summary>
            /// The End of Image marker
            /// </summary>
            public const byte EOI = 0xD9;

            /// <summary>
            /// Application specific marker for marking the jpeg format.
            /// <see href="http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html"/>
            /// </summary>
            public const byte APP0 = 0xE0;

            /// <summary>
            /// Application specific marker for marking where to store metadata.
            /// </summary>
            public const byte APP1 = 0xE1;

            /// <summary>
            /// Application specific marker for marking where to store ICC profile information.
            /// </summary>
            public const byte APP2 = 0xE2;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const byte APP3 = 0xE3;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const byte APP4 = 0xE4;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const byte APP5 = 0xE5;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const byte APP6 = 0xE6;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const byte APP7 = 0xE7;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const byte APP8 = 0xE8;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const byte APP9 = 0xE9;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const byte APP10 = 0xEA;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const byte APP11 = 0xEB;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const byte APP12 = 0xEC;

            /// <summary>
            /// Application specific marker.
            /// </summary>
            public const byte APP13 = 0xED;

            /// <summary>
            /// Application specific marker used by Adobe for storing encoding information for DCT filters.
            /// </summary>
            public const byte APP14 = 0xEE;

            /// <summary>
            /// Application specific marker used by GraphicConverter to store JPEG quality.
            /// </summary>
            public const byte APP15 = 0xEF;

            /// <summary>
            /// The text comment marker
            /// </summary>
            public const byte COM = 0xFE;

            /// <summary>
            /// Define Quantization Table(s) marker
            /// <remarks>
            /// Specifies one or more quantization tables.
            /// </remarks>
            /// </summary>
            public const byte DQT = 0xDB;

            /// <summary>
            /// Start of Frame (baseline DCT)
            /// <remarks>
            /// Indicates that this is a baseline DCT-based JPEG, and specifies the width, height, number of components,
            /// and component subsampling (e.g., 4:2:0).
            /// </remarks>
            /// </summary>
            public const byte SOF0 = 0xC0;

            /// <summary>
            /// Start Of Frame (Extended Sequential DCT)
            /// <remarks>
            /// Indicates that this is a progressive DCT-based JPEG, and specifies the width, height, number of components,
            /// and component subsampling (e.g., 4:2:0).
            /// </remarks>
            /// </summary>
            public const byte SOF1 = 0xC1;

            /// <summary>
            /// Start Of Frame (progressive DCT)
            /// <remarks>
            /// Indicates that this is a progressive DCT-based JPEG, and specifies the width, height, number of components,
            /// and component subsampling (e.g., 4:2:0).
            /// </remarks>
            /// </summary>
            public const byte SOF2 = 0xC2;

            /// <summary>
            /// Define Huffman Table(s)
            /// <remarks>
            /// Specifies one or more Huffman tables.
            /// </remarks>
            /// </summary>
            public const byte DHT = 0xC4;

            /// <summary>
            /// Define Restart Interval
            /// <remarks>
            /// Specifies the interval between RSTn markers, in macroblocks.This marker is followed by two bytes indicating the fixed size so
            /// it can be treated like any other variable size segment.
            /// </remarks>
            /// </summary>
            public const byte DRI = 0xDD;

            /// <summary>
            /// Start of Scan
            /// <remarks>
            /// Begins a top-to-bottom scan of the image. In baseline DCT JPEG images, there is generally a single scan.
            /// Progressive DCT JPEG images usually contain multiple scans. This marker specifies which slice of data it
            /// will contain, and is immediately followed by entropy-coded data.
            /// </remarks>
            /// </summary>
            public const byte SOS = 0xDA;

            /// <summary>
            /// Define First Restart
            /// <remarks>
            /// Inserted every r macroblocks, where r is the restart interval set by a DRI marker.
            /// Not used if there was no DRI marker. The low three bits of the marker code cycle in value from 0 to 7.
            /// </remarks>
            /// </summary>
            public const byte RST0 = 0xD0;

            /// <summary>
            /// Define Eigth Restart
            /// <remarks>
            /// Inserted every r macroblocks, where r is the restart interval set by a DRI marker.
            /// Not used if there was no DRI marker. The low three bits of the marker code cycle in value from 0 to 7.
            /// </remarks>
            /// </summary>
            public const byte RST7 = 0xD7;
        }

        /// <summary>
        /// Contains Adobe specific constants.
        /// </summary>
        internal static class Adobe
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

        /// <summary>
        /// Contains Huffman specific constants.
        /// </summary>
        internal static class Huffman
        {
            /// <summary>
            /// The size of the huffman decoder register.
            /// </summary>
            public const int RegisterSize = 64;

            /// <summary>
            /// The number of bits to fetch when filling the <see cref="HuffmanScanBuffer"/> buffer.
            /// </summary>
            public const int FetchBits = 48;

            /// <summary>
            /// The number of times to read the input stream when filling the <see cref="HuffmanScanBuffer"/> buffer.
            /// </summary>
            public const int FetchLoop = FetchBits / 8;

            /// <summary>
            /// The minimum number of bits allowed before by the <see cref="HuffmanScanBuffer"/> before fetching.
            /// </summary>
            public const int MinBits = RegisterSize - FetchBits;

            /// <summary>
            /// If the next Huffman code is no more than this number of bits, we can obtain its length
            /// and the corresponding symbol directly from this tables.
            /// </summary>
            public const int LookupBits = 8;

            /// <summary>
            /// If a Huffman code is this number of bits we cannot use the lookup table to determine its value.
            /// </summary>
            public const int SlowBits = LookupBits + 1;

            /// <summary>
            /// The size of the lookup table.
            /// </summary>
            public const int LookupSize = 1 << LookupBits;
        }
    }
}
