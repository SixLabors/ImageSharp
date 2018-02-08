using System;

namespace SixLabors.ImageSharp.Tests
{
    internal static class TestDataGenerator
    {
        public static float[] GenerateRandomFloatArray(this Random rnd, int length, float minVal, float maxVal)
        {
            float[] values = new float[length];

            for (int i = 0; i < length; i++)
            {
                values[i] = (float)rnd.NextDouble() * (maxVal - minVal) + minVal;
            }

            return values;
        }

        public static float[] GenerateRandomRoundedFloatArray(this Random rnd, int length, int minVal, int maxValExclusive)
        {
            float[] values = new float[length];

            for (int i = 0; i < length; i++)
            {
                int val = rnd.Next(minVal, maxValExclusive);
                values[i] = (float)val;
            }

            return values;
        }
    }
}