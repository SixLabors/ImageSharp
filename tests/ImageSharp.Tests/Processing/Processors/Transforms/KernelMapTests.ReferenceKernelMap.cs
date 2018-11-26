using System;
using System.Collections.Generic;

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    public partial class KernelMapTests
    {
        /// <summary>
        /// Simplified reference implementation for <see cref="KernelMap"/> functionality.
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

            public static ReferenceKernelMap Calculate(IResampler sampler, int destinationSize, int sourceSize)
            {
                float ratio = (float)sourceSize / destinationSize;
                float scale = ratio;

                if (scale < 1F)
                {
                    scale = 1F;
                }

                float radius = MathF.Ceiling(scale * sampler.Radius);
                
                var result = new List<ReferenceKernel>();

                for (int i = 0; i < destinationSize; i++)
                {
                    float center = ((i + .5F) * ratio) - .5F;

                    // Keep inside bounds.
                    int left = (int)MathF.Ceiling(center - radius);
                    if (left < 0)
                    {
                        left = 0;
                    }

                    int right = (int)MathF.Floor(center + radius);
                    if (right > sourceSize - 1)
                    {
                        right = sourceSize - 1;
                    }

                    float sum = 0;

                    float[] values = new float[right - left + 1];

                    for (int j = left; j <= right; j++)
                    {
                        float weight = sampler.GetValue((j - center) / scale);
                        sum += weight;

                        values[j - left] = weight;
                    }

                    result.Add(new ReferenceKernel(left, values));

                    if (sum > 0)
                    {
                        for (int w = 0; w < values.Length; w++)
                        {
                            values[w] /= sum;
                        }
                    }
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
                return new ReferenceKernel(orig.Left, orig.GetValues().ToArray());
            }
        }
    }
}