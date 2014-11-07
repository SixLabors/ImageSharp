// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Pixel.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------



using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    /// <summary>
    /// The pixel.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct Pixel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Pixel"/> struct.
        /// </summary>
        /// <param name="alpha">
        /// The alpha.
        /// </param>
        /// <param name="red">
        /// The red.
        /// </param>
        /// <param name="green">
        /// The green.
        /// </param>
        /// <param name="blue">
        /// The blue.
        /// </param>
        public Pixel(byte alpha, byte red, byte green, byte blue)
            : this()
        {
            Alpha = alpha;
            Red = red;
            Green = green;
            Blue = blue;

            Debug.Assert(Argb == (alpha << 24 | red << 16 | green << 8 | blue));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pixel"/> struct.
        /// </summary>
        /// <param name="argb">
        /// The argb.
        /// </param>
        public Pixel(int argb)
            : this()
        {
            Argb = argb;
            Debug.Assert(Alpha == ((uint)argb >> 24));
            Debug.Assert(Red == ((uint)(argb >> 16) & 255));
            Debug.Assert(Green == ((uint)(argb >> 8) & 255));
            Debug.Assert(Blue == ((uint)argb & 255));
        }

        /// <summary>
        /// The alpha.
        /// </summary>
        [FieldOffset(3)]
        public byte Alpha;

        /// <summary>
        /// The red.
        /// </summary>
        [FieldOffset(2)]
        public byte Red;

        /// <summary>
        /// The green.
        /// </summary>
        [FieldOffset(1)]
        public byte Green;

        /// <summary>
        /// The blue.
        /// </summary>
        [FieldOffset(0)]
        public byte Blue;

        /// <summary>
        /// The argb.
        /// </summary>
        [FieldOffset(0)]
        public int Argb;

        /// <summary>
        /// The to string.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public override string ToString()
        {
            return string.Format("Alpha:{0} Red:{1} Green:{2} Blue:{3}", Alpha, Red, Green, Blue);
        }
    }
}