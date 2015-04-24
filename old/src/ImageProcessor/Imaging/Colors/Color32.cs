// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Color32.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Structure that defines a 32 bit color
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Colors
{
    using System.Drawing;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Structure that defines a 32 bits per pixel color, used for pixel manipulation not for color conversion.
    /// </summary>
    /// <remarks>
    /// This structure is used to read data from a 32 bits per pixel image
    /// in memory, and is ordered in this manner as this is the way that
    /// the data is laid out in memory
    /// </remarks>
    [StructLayout(LayoutKind.Explicit)]
    public struct Color32
    {
        /// <summary>
        /// Holds the blue component of the colour
        /// </summary>
        [FieldOffset(0)]
        public byte B;

        /// <summary>
        /// Holds the green component of the colour
        /// </summary>
        [FieldOffset(1)]
        public byte G;

        /// <summary>
        /// Holds the red component of the colour
        /// </summary>
        [FieldOffset(2)]
        public byte R;

        /// <summary>
        /// Holds the alpha component of the colour
        /// </summary>
        [FieldOffset(3)]
        public byte A;

        /// <summary>
        /// Permits the color32 to be treated as a 32 bit integer.
        /// </summary>
        [FieldOffset(0)]
        public int Argb;

        /// <summary>
        /// Initializes a new instance of the <see cref="Color32"/> struct.
        /// </summary>
        /// <param name="alpha">
        /// The alpha component.
        /// </param>
        /// <param name="red">
        /// The red component.
        /// </param>
        /// <param name="green">
        /// The green component.
        /// </param>
        /// <param name="blue">
        /// The blue component.
        /// </param>
        public Color32(byte alpha, byte red, byte green, byte blue)
            : this()
        {
            this.A = alpha;
            this.R = red;
            this.G = green;
            this.B = blue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color32"/> struct.
        /// </summary>
        /// <param name="argb">
        /// The combined color components.
        /// </param>
        public Color32(int argb)
            : this()
        {
            this.Argb = argb;
        }

        /// <summary>
        /// Gets the color for this Color32 object
        /// </summary>
        public Color Color
        {
            get { return Color.FromArgb(this.A, this.R, this.G, this.B); }
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <returns>
        /// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false.
        /// </returns>
        /// <param name="obj">Another object to compare to. </param>
        public override bool Equals(object obj)
        {
            if (obj is Color32)
            {
                Color32 color32 = (Color32)obj;

                return this.Argb == color32.Argb;
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
        /// The instance of <see cref="Color32"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Color32 obj)
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