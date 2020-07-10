// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Helper methods that allow the creation of random test data.
    /// </summary>
    internal static class TestDataGenerator
    {
        /// <summary>
        /// Creates an <see cref="float[]"/> of the given length consisting of random values between the two ranges.
        /// </summary>
        /// <param name="rnd">The pseudo-random number generator.</param>
        /// <param name="length">The length.</param>
        /// <param name="minVal">The minimum value.</param>
        /// <param name="maxVal">The maximum value.</param>
        /// <returns>The <see cref="float[]"/>.</returns>
        public static float[] GenerateRandomFloatArray(this Random rnd, int length, float minVal, float maxVal)
        {
            var values = new float[length];

            RandomFill(rnd, values, minVal, maxVal);

            return values;
        }

        public static void RandomFill(this Random rnd, Span<float> destination, float minVal, float maxVal)
        {
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = GetRandomFloat(rnd, minVal, maxVal);
            }
        }

        /// <summary>
        /// Creates an <see cref="Vector4[]"/> of the given length consisting of random values between the two ranges.
        /// </summary>
        /// <param name="rnd">The pseudo-random number generator.</param>
        /// <param name="length">The length.</param>
        /// <param name="minVal">The minimum value.</param>
        /// <param name="maxVal">The maximum value.</param>
        /// <returns>The <see cref="Vector4[]"/>.</returns>
        public static Vector4[] GenerateRandomVectorArray(this Random rnd, int length, float minVal, float maxVal)
        {
            var values = new Vector4[length];

            for (int i = 0; i < length; i++)
            {
                ref Vector4 v = ref values[i];
                v.X = GetRandomFloat(rnd, minVal, maxVal);
                v.Y = GetRandomFloat(rnd, minVal, maxVal);
                v.Z = GetRandomFloat(rnd, minVal, maxVal);
                v.W = GetRandomFloat(rnd, minVal, maxVal);
            }

            return values;
        }

        /// <summary>
        /// Creates an <see cref="float[]"/> of the given length consisting of rounded random values between the two ranges.
        /// </summary>
        /// <param name="rnd">The pseudo-random number generator.</param>
        /// <param name="length">The length.</param>
        /// <param name="minVal">The minimum value.</param>
        /// <param name="maxVal">The maximum value.</param>
        /// <returns>The <see cref="float[]"/>.</returns>
        public static float[] GenerateRandomRoundedFloatArray(this Random rnd, int length, float minVal, float maxVal)
        {
            var values = new float[length];

            for (int i = 0; i < length; i++)
            {
                values[i] = (float)Math.Round(rnd.GetRandomFloat(minVal, maxVal));
            }

            return values;
        }

        /// <summary>
        /// Creates an <see cref="byte[]"/> of the given length consisting of random values.
        /// </summary>
        /// <param name="rnd">The pseudo-random number generator.</param>
        /// <param name="length">The length.</param>
        /// <returns>The <see cref="byte[]"/>.</returns>
        public static byte[] GenerateRandomByteArray(this Random rnd, int length)
        {
            var values = new byte[length];
            rnd.NextBytes(values);
            return values;
        }

        public static short[] GenerateRandomInt16Array(this Random rnd, int length, short minVal, short maxVal)
        {
            var values = new short[length];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (short)rnd.Next(minVal, maxVal);
            }

            return values;
        }

        private static float GetRandomFloat(this Random rnd, float minVal, float maxVal) => ((float)rnd.NextDouble() * (maxVal - minVal)) + minVal;
    }
}
