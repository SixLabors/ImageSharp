// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc.Calculators
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

        public Vector4 Calculate(Vector4 value)
        {
            Vector4.Clamp(value, Vector4.Zero, Vector4.One);

            float[] result;
            switch (this.inputCount)
            {
                case 1:
                    result = this.Interpolate(value.X);
                    break;
                case 2:
                    result = this.Interpolate(value.X, value.Y);
                    break;
                case 3:
                    result = this.Interpolate(value.X, value.Y, value.Z);
                    break;
                case 4:
                    result = this.Interpolate(value.X, value.Y, value.Z, value.W);
                    break;

                default:
                    throw new InvalidOperationException();
            }

            switch (result.Length)
            {
                case 1:
                    return new Vector4(result[0], 0, 0, 0);
                case 2:
                    return new Vector4(result[0], result[1], 0, 0);
                case 3:
                    return new Vector4(result[0], result[1], result[2], 0);
                case 4:
                    return new Vector4(result[0], result[1], result[2], result[3]);

                default:
                    throw new InvalidOperationException();
            }
        }

        private int[] CalculateIndexFactor(int inputCount, byte[] gridPointCount)
        {
            this.indexFactor = new int[inputCount];
            int gpc = 1;
            for (int j = inputCount - 1; j >= 0; j--)
            {
                this.indexFactor[j] = gpc * (gridPointCount[j] - 1);
                gpc *= gridPointCount[j];
            }

            return this.indexFactor;
        }

        private float[] Interpolate(float a)
        {
            return this.Interpolate(new float[] { a });
        }

        private float[] Interpolate(float a, float b)
        {
            return this.Interpolate(new float[] { a, b });
        }

        private float[] Interpolate(float a, float b, float c)
        {
            return this.Interpolate(new float[] { a, b, c });
        }

        private float[] Interpolate(float a, float b, float c, float d)
        {
            return this.Interpolate(new float[] { a, b, c, d });
        }

        private float[] Interpolate(float[] values)
        {
            float[][] nodes = new float[this.nodeCount][];
            for (int i = 0; i < nodes.Length; i++)
            {
                int index = 0;
                for (int j = 0; j < values.Length; j++)
                {
                    float fraction = 1f / (this.gridPointCount[j] - 1);
                    int position = (int)(values[j] / fraction);
                    if (((i >> j) & 1) == 1)
                    {
                        position += 1;
                    }

                    index += (int)((this.indexFactor[j] * (position * fraction)) + 0.5f);
                }

                nodes[i] = this.lut[index];
            }

            float[] factors = new float[this.nodeCount];
            for (int i = 0; i < factors.Length; i++)
            {
                float factor = 1;
                for (int j = 0; j < values.Length; j++)
                {
                    float fraction = 1f / (this.gridPointCount[j] - 1);
                    int position = (int)(values[j] / fraction);

                    float low = position * fraction;
                    float high = (position + 1) * fraction;

                    float mid = high - values[j];
                    float range = high - low;

                    float percentage = mid / range;

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

            float[] output = new float[this.outputCount];
            for (int i = 0; i < output.Length; i++)
            {
                for (int j = 0; j < nodes.Length; j++)
                {
                    output[i] += nodes[j][i] * factors[j];
                }
            }

            return output;
        }
    }
}
