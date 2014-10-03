// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PixelData.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Contains the component parts that make up a single pixel.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    using System.Collections.Generic;

    /// <summary>
    /// Contains the component parts that make up a single pixel.
    /// </summary>
    public struct PixelData
    {
        /// <summary>
        /// The blue component.
        /// </summary>
        public byte B;

        /// <summary>
        /// The green component.
        /// </summary>
        public byte G;

        /// <summary>
        /// The red component.
        /// </summary>
        public byte R;

        /// <summary>
        /// The alpha component.
        /// </summary>
        public byte A;

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <returns>
        /// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false.
        /// </returns>
        /// <param name="obj">Another object to compare to. </param>
        public override bool Equals(object obj)
        {
            if (obj is PixelData)
            {
                PixelData pixelData = (PixelData)obj;

                return this.B == pixelData.B && this.G == pixelData.G & this.R == pixelData.R & this.A == pixelData.A;
            }

            return false;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        public override int GetHashCode()
        {
            return this.GetHashCode(this);
        }

        /// <summary>
        /// Returns the hash code for the given instance.
        /// </summary>
        /// <param name="obj">
        /// The instance of <see cref="PixelData"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(PixelData obj)
        {
            unchecked
            {
                int hashCode = obj.B.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.G.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.R.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.A.GetHashCode();
                return hashCode;
            }
        }
    }
}