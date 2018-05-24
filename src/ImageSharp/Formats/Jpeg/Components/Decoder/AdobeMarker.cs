// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Provides information about the Adobe marker segment.
    /// </summary>
    /// <remarks>See the included 5116.DCT.pdf file in the source for more information.</remarks>
    internal readonly struct AdobeMarker : IEquatable<AdobeMarker>
    {
        /// <summary>
        /// Gets the length of an adobe marker segment.
        /// </summary>
        public const int Length = 12;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdobeMarker"/> struct.
        /// </summary>
        /// <param name="dctEncodeVersion">The DCT encode version</param>
        /// <param name="app14Flags0">The horizontal downsampling hint used for DCT encoding</param>
        /// <param name="app14Flags1">The vertical downsampling hint used for DCT encoding</param>
        /// <param name="colorTransform">The color transform model used</param>
        private AdobeMarker(short dctEncodeVersion, short app14Flags0, short app14Flags1, byte colorTransform)
        {
            this.DCTEncodeVersion = dctEncodeVersion;
            this.APP14Flags0 = app14Flags0;
            this.APP14Flags1 = app14Flags1;
            this.ColorTransform = colorTransform;
        }

        /// <summary>
        /// Gets the DCT Encode Version
        /// </summary>
        public short DCTEncodeVersion { get; }

        /// <summary>
        /// Gets the horizontal downsampling hint used for DCT encoding
        /// 0x0 : (none - Chop)
        /// Bit 15 : Encoded with Blend=1 downsampling
        /// </summary>
        public short APP14Flags0 { get; }

        /// <summary>
        /// Gets the vertical downsampling hint used for DCT encoding
        /// 0x0 : (none - Chop)
        /// Bit 15 : Encoded with Blend=1 downsampling
        /// </summary>
        public short APP14Flags1 { get; }

        /// <summary>
        /// Gets the colorspace transform model used
        /// 00 : Unknown (RGB or CMYK)
        /// 01 : YCbCr
        /// 02 : YCCK
        /// </summary>
        public byte ColorTransform { get; }

        /// <summary>
        /// Converts the specified byte array representation of an Adobe marker to its <see cref="AdobeMarker"/> equivalent and
        /// returns a value that indicates whether the conversion succeeded.
        /// </summary>
        /// <param name="bytes">The byte array containing metadata to parse</param>
        /// <param name="marker">The marker to return.</param>
        public static bool TryParse(byte[] bytes, out AdobeMarker marker)
        {
            if (ProfileResolver.IsProfile(bytes, ProfileResolver.AdobeMarker))
            {
                short dctEncodeVersion = (short)((bytes[5] << 8) | bytes[6]);
                short app14Flags0 = (short)((bytes[7] << 8) | bytes[8]);
                short app14Flags1 = (short)((bytes[9] << 8) | bytes[10]);
                byte colorTransform = bytes[11];

                marker = new AdobeMarker(dctEncodeVersion, app14Flags0, app14Flags1, colorTransform);
                return true;
            }

            marker = default;
            return false;
        }

        /// <inheritdoc/>
        public bool Equals(AdobeMarker other)
        {
            return this.DCTEncodeVersion == other.DCTEncodeVersion
                && this.APP14Flags0 == other.APP14Flags0
                && this.APP14Flags1 == other.APP14Flags1
                && this.ColorTransform == other.ColorTransform;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is AdobeMarker other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashHelpers.Combine(
                this.DCTEncodeVersion.GetHashCode(),
                HashHelpers.Combine(
                    this.APP14Flags0.GetHashCode(),
                    HashHelpers.Combine(
                        this.APP14Flags1.GetHashCode(),
                        this.ColorTransform.GetHashCode())));
        }
    }
}