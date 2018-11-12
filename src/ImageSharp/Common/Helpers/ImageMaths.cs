// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Provides common mathematical methods.
    /// </summary>
    internal static class ImageMaths
    {
        /// <summary>
        /// Gets the luminance from the rgb components using the formula as specified by ITU-R Recommendation BT.709.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <returns>The <see cref="byte"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static byte Get8BitBT709Luminance(byte r, byte g, byte b) => (byte)((r * .2126F) + (g * .7152F) + (b * .0722F) + 0.5f);

        /// <summary>
        /// Gets the luminance from the rgb components using the formula as specified by ITU-R Recommendation BT.709.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <returns>The <see cref="ushort"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static ushort Get16BitBT709Luminance(ushort r, ushort g, ushort b) => (ushort)((r * .2126F) + (g * .7152F) + (b * .0722F));

        /// <summary>
        /// Scales a value from a 16 bit <see cref="ushort"/> to it's 8 bit <see cref="byte"/> equivalent.
        /// </summary>
        /// <param name="component">The 8 bit component value.</param>
        /// <returns>The <see cref="byte"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static byte DownScaleFrom16BitTo8Bit(ushort component)
        {
            // To scale to 8 bits From a 16-bit value V the required value (from the PNG specification) is:
            //
            //    (V * 255) / 65535
            //
            // This reduces to round(V / 257), or floor((V + 128.5)/257)
            //
            // Represent V as the two byte value vhi.vlo.  Make a guess that the
            // result is the top byte of V, vhi, then the correction to this value
            // is:
            //
            //    error = floor(((V-vhi.vhi) + 128.5) / 257)
            //          = floor(((vlo-vhi) + 128.5) / 257)
            //
            // This can be approximated using integer arithmetic (and a signed
            // shift):
            //
            //    error = (vlo-vhi+128) >> 8;
            //
            // The approximate differs from the exact answer only when (vlo-vhi) is
            // 128; it then gives a correction of +1 when the exact correction is
            // 0.  This gives 128 errors.  The exact answer (correct for all 16-bit
            // input values) is:
            //
            //    error = (vlo-vhi+128)*65535 >> 24;
            //
            // An alternative arithmetic calculation which also gives no errors is:
            //
            //    (V * 255 + 32895) >> 16
            return (byte)(((component * 255) + 32895) >> 16);
        }

        /// <summary>
        /// Scales a value from an 8 bit <see cref="byte"/> to it's 16 bit <see cref="ushort"/> equivalent.
        /// </summary>
        /// <param name="component">The 8 bit component value.</param>
        /// <returns>The <see cref="ushort"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static ushort UpscaleFrom8BitTo16Bit(byte component) => (ushort)(component * 257);

        /// <summary>
        /// Determine the Greatest CommonDivisor (GCD) of two numbers.
        /// </summary>
        public static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }

            return a;
        }

        /// <summary>
        /// Determine the Least Common Multiple (LCM) of two numbers.
        /// TODO: This method might be useful for building a more compact <see cref="Processing.Processors.Transforms.KernelMap"/>
        /// </summary>
        public static int LeastCommonMultiple(int a, int b)
        {
            // https://en.wikipedia.org/wiki/Least_common_multiple#Reduction_by_the_greatest_common_divisor
            return (a / GreatestCommonDivisor(a, b)) * b;
        }

        /// <summary>
        /// Calculates <paramref name="x"/> % 4
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int Modulo4(int x) => x & 3;

        /// <summary>
        /// Calculates <paramref name="x"/> % 8
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int Modulo8(int x) => x & 7;

        /// <summary>
        /// Fast (x mod m) calculator, with the restriction that
        /// <paramref name="m"/> should be power of 2.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int ModuloP2(int x, int m)
        {
            return x & (m - 1);
        }

        /// <summary>
        /// Returns the absolute value of a 32-bit signed integer. Uses bit shifting to speed up the operation.
        /// </summary>
        /// <param name="x">
        /// A number that is greater than <see cref="int.MinValue"/>, but less than or equal to <see cref="int.MaxValue"/>
        /// </param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int FastAbs(int x)
        {
            int y = x >> 31;
            return (x ^ y) - y;
        }

        /// <summary>
        /// Returns a specified number raised to the power of 2
        /// </summary>
        /// <param name="x">A single-precision floating-point number</param>
        /// <returns>The number <paramref name="x" /> raised to the power of 2.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float Pow2(float x) => x * x;

        /// <summary>
        /// Returns a specified number raised to the power of 3
        /// </summary>
        /// <param name="x">A single-precision floating-point number</param>
        /// <returns>The number <paramref name="x" /> raised to the power of 3.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float Pow3(float x) => x * x * x;

        /// <summary>
        /// Returns how many bits are required to store the specified number of colors.
        /// Performs a Log2() on the value.
        /// </summary>
        /// <param name="colors">The number of colors.</param>
        /// <returns>
        /// The <see cref="int"/>
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int GetBitsNeededForColorDepth(int colors) => Math.Max(1, (int)Math.Ceiling(Math.Log(colors, 2)));

        /// <summary>
        /// Returns how many colors will be created by the specified number of bits.
        /// </summary>
        /// <param name="bitDepth">The bit depth.</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int GetColorCountForBitDepth(int bitDepth) => 1 << bitDepth;

        /// <summary>
        /// Implementation of 1D Gaussian G(x) function
        /// </summary>
        /// <param name="x">The x provided to G(x).</param>
        /// <param name="sigma">The spread of the blur.</param>
        /// <returns>The Gaussian G(x)</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float Gaussian(float x, float sigma)
        {
            const float Numerator = 1.0f;
            float denominator = MathF.Sqrt(2 * MathF.PI) * sigma;

            float exponentNumerator = -x * x;
            float exponentDenominator = 2 * Pow2(sigma);

            float left = Numerator / denominator;
            float right = MathF.Exp(exponentNumerator / exponentDenominator);

            return left * right;
        }

        /// <summary>
        /// Returns the result of a normalized sine cardinal function for the given value.
        /// SinC(x) = sin(pi*x)/(pi*x).
        /// </summary>
        /// <param name="f">A single-precision floating-point number to calculate the result for.</param>
        /// <returns>
        /// The sine cardinal of <paramref name="f" />.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float SinC(float f)
        {
            if (MathF.Abs(f) > Constants.Epsilon)
            {
                f *= MathF.PI;
                float result = MathF.Sin(f) / f;
                return MathF.Abs(result) < Constants.Epsilon ? 0F : result;
            }

            return 1F;
        }

        /// <summary>
        /// Returns the result of a B-C filter against the given value.
        /// <see href="http://www.imagemagick.org/Usage/filter/#cubic_bc"/>
        /// </summary>
        /// <param name="x">The value to process.</param>
        /// <param name="b">The B-Spline curve variable.</param>
        /// <param name="c">The Cardinal curve variable.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float GetBcValue(float x, float b, float c)
        {
            if (x < 0F)
            {
                x = -x;
            }

            float temp = x * x;
            if (x < 1F)
            {
                x = ((12 - (9 * b) - (6 * c)) * (x * temp)) + ((-18 + (12 * b) + (6 * c)) * temp) + (6 - (2 * b));
                return x / 6F;
            }

            if (x < 2F)
            {
                x = ((-b - (6 * c)) * (x * temp)) + (((6 * b) + (30 * c)) * temp) + (((-12 * b) - (48 * c)) * x) + ((8 * b) + (24 * c));
                return x / 6F;
            }

            return 0F;
        }

        /// <summary>
        /// Gets the bounding <see cref="Rectangle"/> from the given points.
        /// </summary>
        /// <param name="topLeft">
        /// The <see cref="Point"/> designating the top left position.
        /// </param>
        /// <param name="bottomRight">
        /// The <see cref="Point"/> designating the bottom right position.
        /// </param>
        /// <returns>
        /// The bounding <see cref="Rectangle"/>.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Rectangle GetBoundingRectangle(Point topLeft, Point bottomRight) => new Rectangle(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

        /// <summary>
        /// Finds the bounding rectangle based on the first instance of any color component other
        /// than the given one.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="bitmap">The <see cref="Image{TPixel}"/> to search within.</param>
        /// <param name="componentValue">The color component value to remove.</param>
        /// <param name="channel">The <see cref="RgbaComponent"/> channel to test against.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        public static Rectangle GetFilteredBoundingRectangle<TPixel>(ImageFrame<TPixel> bitmap, float componentValue, RgbaComponent channel = RgbaComponent.B)
            where TPixel : struct, IPixel<TPixel>
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            Point topLeft = default;
            Point bottomRight = default;

            Func<ImageFrame<TPixel>, int, int, float, bool> delegateFunc;

            // Determine which channel to check against
            switch (channel)
            {
                case RgbaComponent.R:
                    delegateFunc = (pixels, x, y, b) => MathF.Abs(pixels[x, y].ToVector4().X - b) > Constants.Epsilon;
                    break;

                case RgbaComponent.G:
                    delegateFunc = (pixels, x, y, b) => MathF.Abs(pixels[x, y].ToVector4().Y - b) > Constants.Epsilon;
                    break;

                case RgbaComponent.B:
                    delegateFunc = (pixels, x, y, b) => MathF.Abs(pixels[x, y].ToVector4().Z - b) > Constants.Epsilon;
                    break;

                default:
                    delegateFunc = (pixels, x, y, b) => MathF.Abs(pixels[x, y].ToVector4().W - b) > Constants.Epsilon;
                    break;
            }

            int GetMinY(ImageFrame<TPixel> pixels)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return y;
                        }
                    }
                }

                return 0;
            }

            int GetMaxY(ImageFrame<TPixel> pixels)
            {
                for (int y = height - 1; y > -1; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return y;
                        }
                    }
                }

                return height;
            }

            int GetMinX(ImageFrame<TPixel> pixels)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return x;
                        }
                    }
                }

                return 0;
            }

            int GetMaxX(ImageFrame<TPixel> pixels)
            {
                for (int x = width - 1; x > -1; x--)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return x;
                        }
                    }
                }

                return height;
            }

            topLeft.Y = GetMinY(bitmap);
            topLeft.X = GetMinX(bitmap);
            bottomRight.Y = (GetMaxY(bitmap) + 1).Clamp(0, height);
            bottomRight.X = (GetMaxX(bitmap) + 1).Clamp(0, width);

            return GetBoundingRectangle(topLeft, bottomRight);
        }
    }
}