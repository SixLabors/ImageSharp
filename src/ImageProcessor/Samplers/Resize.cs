// <copyright file="Resize.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides methods that allow the resizing of images using various resampling algorithms.
    /// <remarks>
    /// TODO: There is a bug in this class. Whenever the processor is set to use parallel processing, the output image becomes distorted
    /// at the join points when startY is greater than 0. Uncomment the Parallelism overload and run the ImageShouldResize method in the SamplerTests
    /// class to see the error manifest.
    /// It is imperative that the issue is solved or resampling will be too slow to be practical and the project will have to cease.
    /// </remarks>
    /// </summary>
    public class Resize : ParallelImageProcessor
    {
        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// Initializes a new instance of the <see cref="Resize"/> class.
        /// </summary>
        /// <param name="sampler">
        /// The sampler to perform the resize operation.
        /// </param>
        public Resize(IResampler sampler)
        {
            Guard.NotNull(sampler, nameof(sampler));

            this.Sampler = sampler;
        }

        /// <inheritdoc/>
        public override int Parallelism => 1; // Uncomment this to see bug.

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int sourceWidth = source.Width;
            int sourceHeight = source.Height;

            int width = target.Width;
            int height = target.Height;

            int targetY = targetRectangle.Y;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;

            // Scaling factors
            double heightFactor = sourceHeight / (double)targetRectangle.Height;
            int targetSectionHeight = endY - startY;
            int sourceSectionHeight = (int)((targetSectionHeight * heightFactor) + .5);

            int offsetY = this.CalculateOffset(startY, targetSectionHeight, sourceSectionHeight);
            int offsetX = this.CalculateOffset(startX, targetRectangle.Width, sourceRectangle.Width);
            Weights[] horizontalWeights = this.PrecomputeWeights(targetRectangle.Width, sourceRectangle.Width);
            Weights[] verticalWeights = this.PrecomputeWeights(targetSectionHeight, sourceSectionHeight);

            // Width and height decreased by 1
            int maxHeight = sourceHeight - 1;
            int maxWidth = sourceWidth - 1;

            for (int y = startY; y < endY; y++)
            {
                if (y >= 0 && y < height)
                {
                    List<Weight> verticalValues = verticalWeights[y - startY].Values;
                    double verticalSum = verticalWeights[y - startY].Sum;

                    for (int x = startX; x < endX; x++)
                    {
                        if (x >= 0 && x < width)
                        {
                            List<Weight> horizontalValues = horizontalWeights[x - startX].Values;
                            double horizontalSum = horizontalWeights[x - startX].Sum;

                            // Destination color components
                            double r = 0;
                            double g = 0;
                            double b = 0;
                            double a = 0;

                            foreach (Weight yw in verticalValues)
                            {
                                if (Math.Abs(yw.Value) < Epsilon)
                                {
                                    continue;
                                }

                                // TODO: This offset is wrong.
                                int originY = offsetY == 0 ? yw.Index : yw.Index + offsetY;
                                originY = originY.Clamp(0, maxHeight);

                                foreach (Weight xw in horizontalValues)
                                {
                                    if (Math.Abs(xw.Value) < Epsilon)
                                    {
                                        continue;
                                    }

                                    // TODO: This offset is wrong.
                                    int originX = xw.Index + offsetX;
                                    originX = originX.Clamp(0, maxWidth);

                                    Bgra sourceColor = source[originX, originY];
                                    sourceColor = PixelOperations.ToLinear(sourceColor);

                                    r += sourceColor.R * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                    g += sourceColor.G * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                    b += sourceColor.B * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                    a += sourceColor.A * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                }
                            }

                            Bgra destinationColor = new Bgra(b.ToByte(), g.ToByte(), r.ToByte(), a.ToByte());
                            destinationColor = PixelOperations.ToSrgb(destinationColor);
                            target[x, y] = destinationColor;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Computes the weights to apply at each pixel when resizing.
        /// </summary>
        /// <param name="destinationSize">The destination section size.</param>
        /// <param name="sourceSize">The source section size.</param>
        /// <returns>
        /// The <see cref="T:Weights[]"/>.
        /// </returns>
        private Weights[] PrecomputeWeights(int destinationSize, int sourceSize)
        {
            IResampler sampler = this.Sampler;
            double du = sourceSize / (double)destinationSize;
            double scale = du;

            if (scale < 1)
            {
                scale = 1;
            }

            double ru = Math.Ceiling(scale * sampler.Radius);
            Weights[] result = new Weights[destinationSize];

            for (int i = 0; i < destinationSize; i++)
            {
                double fu = ((i + .5) * du) - 0.5;
                int startU = (int)Math.Ceiling(fu - ru);

                if (startU < 0)
                {
                    startU = 0;
                }

                int endU = (int)Math.Floor(fu + ru);

                if (endU > sourceSize - 1)
                {
                    endU = sourceSize - 1;
                }

                double sum = 0;
                result[i] = new Weights();

                for (int a = startU; a <= endU; a++)
                {
                    double w = 255 * sampler.GetValue((a - fu) / scale);

                    if (Math.Abs(w) > Epsilon)
                    {
                        sum += w;
                        result[i].Values.Add(new Weight(a, w));
                    }
                }

                result[i].Sum = sum;
            }

            return result;
        }

        /// <summary>
        /// Calculates the scaled offset caused by parallelism.
        /// </summary>
        /// <param name="offset">The offset position.</param>
        /// <param name="destinationSize">The destination size.</param>
        /// <param name="sourceSize">The source size.</param>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        private int CalculateOffset(int offset, int destinationSize, int sourceSize)
        {
            if (offset == 0)
            {
                return 0;
            }

            IResampler sampler = this.Sampler;
            double du = sourceSize / (double)destinationSize;
            double scale = du;

            if (scale < 1)
            {
                scale = 1;
            }

            double ru = Math.Ceiling(scale * sampler.Radius);

            double fu = ((offset + .5) * du) - 0.5;
            int result = (int)Math.Ceiling(fu - ru);

            if (result < 0)
            {
                return 0;
            }

            return result;
        }

        /// <summary>
        /// Represents the weight to be added to a scaled pixel.
        /// </summary>
        protected struct Weight
        {
            /// <summary>
            /// The pixel index.
            /// </summary>
            public readonly int Index;

            /// <summary>
            /// The result of the interpolation algorithm.
            /// </summary>
            public readonly double Value;

            /// <summary>
            /// Initializes a new instance of the <see cref="Weight"/> struct.
            /// </summary>
            /// <param name="index">The index.</param>
            /// <param name="value">The value.</param>
            public Weight(int index, double value)
            {
                this.Index = index;
                this.Value = value;
            }
        }

        /// <summary>
        /// Represents a collection of weights and their sum.
        /// </summary>
        protected class Weights
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Weights"/> class.
            /// </summary>
            public Weights()
            {
                this.Values = new List<Weight>();
            }

            /// <summary>
            /// Gets or sets the values.
            /// </summary>
            public List<Weight> Values { get; set; }

            /// <summary>
            /// Gets or sets the sum.
            /// </summary>
            public double Sum { get; set; }
        }
    }
}
