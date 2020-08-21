// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    public partial class ResizeKernelMapTests
    {
        /// <summary>
        /// Simplified reference implementation for <see cref="ResizeKernelMap"/> functionality.
        /// </summary>
        internal class ReferenceKernelMap
        {
            private readonly ReferenceKernel[] kernels;

            public ReferenceKernelMap(ReferenceKernel[] kernels)
            {
                this.kernels = kernels;
            }

            public int DestinationSize => this.kernels.Length;

            public ReferenceKernel GetKernel(int destinationIndex) => this.kernels[destinationIndex];

            public static ReferenceKernelMap Calculate<TResampler>(in TResampler sampler, int destinationSize, int sourceSize, bool normalize = true)
                where TResampler : struct, IResampler
            {
                double ratio = (double)sourceSize / destinationSize;
                double scale = ratio;

                if (scale < 1F)
                {
                    scale = 1F;
                }

                TolerantMath tolerantMath = TolerantMath.Default;

                double radius = tolerantMath.Ceiling(scale * sampler.Radius);

                var result = new List<ReferenceKernel>();

                for (int i = 0; i < destinationSize; i++)
                {
                    double center = ((i + .5) * ratio) - .5;

                    // Keep inside bounds.
                    int left = (int)tolerantMath.Ceiling(center - radius);
                    if (left < 0)
                    {
                        left = 0;
                    }

                    int right = (int)tolerantMath.Floor(center + radius);
                    if (right > sourceSize - 1)
                    {
                        right = sourceSize - 1;
                    }

                    double sum = 0;

                    double[] values = new double[right - left + 1];

                    for (int j = left; j <= right; j++)
                    {
                        double weight = sampler.GetValue((float)((j - center) / scale));
                        sum += weight;

                        values[j - left] = weight;
                    }

                    if (sum > 0 && normalize)
                    {
                        for (int w = 0; w < values.Length; w++)
                        {
                            values[w] /= sum;
                        }
                    }

                    float[] floatVals = values.Select(v => (float)v).ToArray();

                    result.Add(new ReferenceKernel(left, floatVals));
                }

                return new ReferenceKernelMap(result.ToArray());
            }
        }

        internal struct ReferenceKernel
        {
            public ReferenceKernel(int left, float[] values)
            {
                this.Left = left;
                this.Values = values;
            }

            public int Left { get; }

            public float[] Values { get; }

            public int Length => this.Values.Length;

            public static implicit operator ReferenceKernel(ResizeKernel orig)
            {
                return new ReferenceKernel(orig.StartIndex, orig.Values.ToArray());
            }
        }
    }
}
