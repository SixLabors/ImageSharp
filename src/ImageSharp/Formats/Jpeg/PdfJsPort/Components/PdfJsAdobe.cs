// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Provides information about the Adobe marker segment
    /// </summary>
    internal struct PdfJsAdobe : IEquatable<PdfJsAdobe>
    {
        /// <summary>
        /// The DCT Encode Version
        /// </summary>
        public short DCTEncodeVersion;

        /// <summary>
        /// 0x0 : (none)
        /// Bit 15 : Encoded with Blend=1 downsampling
        /// </summary>
        public short APP14Flags0;

        /// <summary>
        /// 0x0 : (none)
        /// </summary>
        public short APP14Flags1;

        /// <summary>
        /// Determines the colorspace transform
        /// 00 : Unknown (RGB or CMYK)
        /// 01 : YCbCr
        /// 02 : YCCK
        /// </summary>
        public byte ColorTransform;

        /// <inheritdoc/>
        public bool Equals(PdfJsAdobe other)
        {
            return this.DCTEncodeVersion == other.DCTEncodeVersion
                && this.APP14Flags0 == other.APP14Flags0
                && this.APP14Flags1 == other.APP14Flags1
                && this.ColorTransform == other.ColorTransform;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is PdfJsAdobe && this.Equals((PdfJsAdobe)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                // TODO: Merge and use HashCodeHelpers
                int hashCode = this.DCTEncodeVersion.GetHashCode();
                hashCode = (hashCode * 397) ^ this.APP14Flags0.GetHashCode();
                hashCode = (hashCode * 397) ^ this.APP14Flags1.GetHashCode();
                hashCode = (hashCode * 397) ^ this.ColorTransform.GetHashCode();
                return hashCode;
            }
        }
    }
}