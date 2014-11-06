// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Box.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The box for storing color attributes.
//   Adapted from <see href="https://github.com/drewnoakes" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    /// <summary>
    /// The box for storing color attributes.
    /// Adapted from <see href="https://github.com/drewnoakes"/>
    /// </summary>
    public struct Box
    {
        /// <summary>
        /// The alpha maximum.
        /// </summary>
        public byte AlphaMaximum;

        /// <summary>
        /// The alpha minimum.
        /// </summary>
        public byte AlphaMinimum;

        /// <summary>
        /// The blue maximum.
        /// </summary>
        public byte BlueMaximum;

        /// <summary>
        /// The blue minimum.
        /// </summary>
        public byte BlueMinimum;

        /// <summary>
        /// The green maximum.
        /// </summary>
        public byte GreenMaximum;

        /// <summary>
        /// The green minimum.
        /// </summary>
        public byte GreenMinimum;

        /// <summary>
        /// The red maximum.
        /// </summary>
        public byte RedMaximum;

        /// <summary>
        /// The red minimum.
        /// </summary>
        public byte RedMinimum;

        /// <summary>
        /// The size.
        /// </summary>
        public int Size;
    }
}