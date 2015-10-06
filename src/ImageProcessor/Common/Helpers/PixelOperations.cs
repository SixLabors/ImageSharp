// <copyright file="PixelOperations.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;

    /// <summary>
    /// Performs per-pixel operations.
    /// </summary>
    public static class PixelOperations
    {
        /// <summary>
        /// The array of bytes representing each possible value of color component
        /// converted from sRGB to the linear color space.
        /// </summary>
        private static readonly Lazy<byte[]> LinearBytes = new Lazy<byte[]>(GetLinearBytes);

        /// <summary>
        /// The array of bytes representing each possible value of color component
        /// converted from linear to the sRGB color space.
        /// </summary>
        private static readonly Lazy<byte[]> SrgbBytes = new Lazy<byte[]>(GetSrgbBytes);

        /// <summary>
        /// Converts an pixel from an sRGB color-space to the equivalent linear color-space.
        /// </summary>
        /// <param name="composite">
        /// The <see cref="Bgra"/> to convert.
        /// </param>
        /// <returns>
        /// The <see cref="Bgra"/>.
        /// </returns>
        public static Bgra ToLinear(Bgra composite)
        {
            // Create only once and lazily.
            byte[] ramp = LinearBytes.Value;

            return new Bgra(composite.B, ramp[composite.G], ramp[composite.R], ramp[composite.A]);
        }

        /// <summary>
        /// Converts a pixel from a linear color-space to the equivalent sRGB color-space.
        /// </summary>
        /// <param name="linear">
        /// The <see cref="Bgra"/> to convert.
        /// </param>
        /// <returns>
        /// The <see cref="Bgra"/>.
        /// </returns>
        public static Bgra ToSrgb(Bgra linear)
        {
            // Create only once and lazily.
            byte[] ramp = SrgbBytes.Value;

            return new Bgra(linear.B, ramp[linear.G], ramp[linear.R], ramp[linear.A]);
        }

        /// <summary>
        /// Gets an array of bytes representing each possible value of color component
        /// converted from sRGB to the linear color space.
        /// </summary>
        /// <returns>
        /// The <see cref="T:byte[]"/>.
        /// </returns>
        private static byte[] GetLinearBytes()
        {
            byte[] ramp = new byte[256];
            for (int x = 0; x < 256; ++x)
            {
                byte val = (byte)(255f * SrgbToLinear(x / 255f)).Clamp(0, 255);
                ramp[x] = val;
            }

            return ramp;
        }

        /// <summary>
        /// Gets an array of bytes representing each possible value of color component
        /// converted from linear to the sRGB color space.
        /// </summary>
        /// <returns>
        /// The <see cref="T:byte[]"/>.
        /// </returns>
        private static byte[] GetSrgbBytes()
        {
            byte[] ramp = new byte[256];
            for (int x = 0; x < 256; ++x)
            {
                byte val = (byte)(255f * LinearToSrgb(x / 255f)).Clamp(0, 255);
                ramp[x] = val;
            }

            return ramp;
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
            float a = 0.055f;

            if (signal <= 0.04045)
            {
                return signal / 12.92f;
            }

            return (float)Math.Pow((signal + a) / (1 + a), 2.4);
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
            float a = 0.055f;

            if (signal <= 0.0031308)
            {
                return signal * 12.92f;
            }

            return ((float)((1 + a) * Math.Pow(signal, 1 / 2.4f))) - a;
        }
    }
}