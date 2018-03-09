// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Transforms.Resamplers;

namespace SixLabors.ImageSharp.Processing.Transforms.Processors
{
    /// <summary>
    /// The base class for performing interpolated affine and non-affine transforms.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class InterpolatedTransformProcessorBase<TPixel> : TransformProcessorBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InterpolatedTransformProcessorBase{TPixel}"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        protected InterpolatedTransformProcessorBase(IResampler sampler)
        {
            Guard.NotNull(sampler, nameof(sampler));
            this.Sampler = sampler;
        }

        /// <summary>
        /// Gets the sampler to perform interpolation of the transform operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <summary>
        /// Calculated the weights for the given point.
        /// This method uses more samples than the upscaled version to ensure edge pixels are correctly rendered.
        /// Additionally the weights are normalized.
        /// </summary>
        /// <param name="min">The minimum sampling offset</param>
        /// <param name="max">The maximum sampling offset</param>
        /// <param name="sourceMin">The minimum source bounds</param>
        /// <param name="sourceMax">The maximum source bounds</param>
        /// <param name="point">The transformed point dimension</param>
        /// <param name="sampler">The sampler</param>
        /// <param name="scale">The transformed image scale relative to the source</param>
        /// <param name="weights">The collection of weights</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void CalculateWeightsDown(int min, int max, int sourceMin, int sourceMax, float point, IResampler sampler, float scale, Span<float> weights)
        {
            float sum = 0;
            ref float weightsBaseRef = ref weights[0];

            // Downsampling weights requires more edge sampling plus normalization of the weights
            for (int x = 0, i = min; i <= max; i++, x++)
            {
                int index = i;
                if (index < sourceMin)
                {
                    index = sourceMin;
                }

                if (index > sourceMax)
                {
                    index = sourceMax;
                }

                float weight = sampler.GetValue((index - point) / scale);
                sum += weight;
                Unsafe.Add(ref weightsBaseRef, x) = weight;
            }

            if (sum > 0)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    ref float wRef = ref Unsafe.Add(ref weightsBaseRef, i);
                    wRef = wRef / sum;
                }
            }
        }

        /// <summary>
        /// Calculated the weights for the given point.
        /// </summary>
        /// <param name="sourceMin">The minimum source bounds</param>
        /// <param name="sourceMax">The maximum source bounds</param>
        /// <param name="point">The transformed point dimension</param>
        /// <param name="sampler">The sampler</param>
        /// <param name="weights">The collection of weights</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void CalculateWeightsScaleUp(int sourceMin, int sourceMax, float point, IResampler sampler, Span<float> weights)
        {
            ref float weightsBaseRef = ref weights[0];
            for (int x = 0, i = sourceMin; i <= sourceMax; i++, x++)
            {
                float weight = sampler.GetValue(i - point);
                Unsafe.Add(ref weightsBaseRef, x) = weight;
            }
        }

        /// <summary>
        /// Calculates the sampling radius for the current sampler
        /// </summary>
        /// <param name="sourceSize">The source dimension size</param>
        /// <param name="destinationSize">The destination dimension size</param>
        /// <returns>The radius, and scaling factor</returns>
        protected (float radius, float scale, float ratio) GetSamplingRadius(int sourceSize, int destinationSize)
        {
            float ratio = (float)sourceSize / destinationSize;
            float scale = ratio;

            if (scale < 1F)
            {
                scale = 1F;
            }

            return (MathF.Ceiling(scale * this.Sampler.Radius), scale, ratio);
        }
    }
}