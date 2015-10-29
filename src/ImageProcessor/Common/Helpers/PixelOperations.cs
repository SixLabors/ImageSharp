// <copyright file="PixelOperations.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Performs per-pixel operations.
    /// </summary>
    public static class PixelOperations
    {
        /// <summary>
        /// Converts an pixel from an sRGB color-space to the equivalent linear color-space.
        /// </summary>
        /// <param name="composite">
        /// The <see cref="Bgra32"/> to convert.
        /// </param>
        /// <returns>
        /// The <see cref="Bgra32"/>.
        /// </returns>
        public static Color ToLinear(Color composite)
        {
            // TODO: Figure out a way to either cache these values quickly or perform the calcuations together.
            composite.R = SrgbToLinear(composite.R);
            composite.G = SrgbToLinear(composite.G);
            composite.B = SrgbToLinear(composite.B);

            return composite;
        }

        /// <summary>
        /// Converts a pixel from a linear color-space to the equivalent sRGB color-space.
        /// </summary>
        /// <param name="linear">
        /// The <see cref="Bgra32"/> to convert.
        /// </param>
        /// <returns>
        /// The <see cref="Bgra32"/>.
        /// </returns>
        public static Color ToSrgb(Color linear)
        {
            // TODO: Figure out a way to either cache these values quickly or perform the calcuations together.
            linear.R = LinearToSrgb(linear.R);
            linear.G = LinearToSrgb(linear.G);
            linear.B = LinearToSrgb(linear.B);

            return linear;
        }

        /// <summary>
        /// Gets the correct linear value from an sRGB signal.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="signal">The signal value to convert.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float SrgbToLinear(float signal)
        {
            if (signal <= 0.04045f)
            {
                return signal / 12.92f;
            }

            return (float)Math.Pow((signal + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>
        /// Gets the correct sRGB value from an linear signal.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="signal">The signal value to convert.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float LinearToSrgb(float signal)
        {
            if (signal <= 0.0031308f)
            {
                return signal * 12.92f;
            }

            return (1.055f * (float)Math.Pow(signal, 0.41666666f)) - 0.055f;
        }
    }
}