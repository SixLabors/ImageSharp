// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Icc
{
    internal class ClutCalculator : IVector4Calculator
    {
        private int inputCount;
        private int outputCount;
        private float[][] lut;
        private byte[] gridPointCount;
        private int[] indexFactor;
        private int nodeCount;

        public ClutCalculator(IccClut clut)
        {
            Guard.NotNull(clut, nameof(clut));

            this.inputCount = clut.InputChannelCount;
            this.outputCount = clut.OutputChannelCount;
            this.lut = clut.Values;
            this.gridPointCount = clut.GridPointCount;
            this.indexFactor = this.CalculateIndexFactor(clut.InputChannelCount, clut.GridPointCount);
            this.nodeCount = (int)Math.Pow(2, clut.InputChannelCount);
        }

        public unsafe Vector4 Calculate(Vector4 value)
        {
            value = Vector4.Clamp(value, Vector4.Zero, Vector4.One);

            Vector4 result = default;
            this.Interpolate((float*)&value, this.inputCount, (float*)&result, this.outputCount);

            return result;
        }

        private int[] CalculateIndexFactor(int inputCount, byte[] gridPointCount)
        {
            int[] factors = new int[inputCount];
            int gpc = 1;
            for (int j = inputCount - 1; j >= 0; j--)
            {
                factors[j] = gpc * (gridPointCount[j] - 1);
                gpc *= gridPointCount[j];
            }

            return factors;
        }

        private unsafe void Interpolate(float* values, int valueLength, float* result, int resultLength)
        {
            float[][] nodes = new float[this.nodeCount][];
            for (int i = 0; i < nodes.Length; i++)
            {
                int index = 0;
                for (int j = 0; j < valueLength; j++)
                {
                    float fraction = 1f / (this.gridPointCount[j] - 1);
                    int position = (int)(values[j] / fraction) + ((i >> j) & 1);
                    index += (int)((this.indexFactor[j] * (position * fraction)) + 0.5f);
                }

                nodes[i] = this.lut[index];
            }

            Span<float> factors = stackalloc float[this.nodeCount];
            for (int i = 0; i < factors.Length; i++)
            {
                float factor = 1;
                for (int j = 0; j < valueLength; j++)
                {
                    float fraction = 1f / (this.gridPointCount[j] - 1);
                    int position = (int)(values[j] / fraction);

                    float low = position * fraction;
                    float high = (position + 1) * fraction;
                    float percentage = (high - values[j]) / (high - low);

                    if (((i >> j) & 1) == 1)
                    {
                        factor *= percentage;
                    }
                    else
                    {
                        factor *= 1 - percentage;
                    }
                }

                factors[i] = factor;
            }

            for (int i = 0; i < resultLength; i++)
            {
                for (int j = 0; j < nodes.Length; j++)
                {
                    result[i] += nodes[j][i] * factors[j];
                }
            }
        }
    }
}
