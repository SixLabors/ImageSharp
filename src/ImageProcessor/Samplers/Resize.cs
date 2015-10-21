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

            Weights[] horizontalWeights = this.PrecomputeWeights(targetRectangle.Width, sourceRectangle.Width, this.Sampler);
            Weights[] verticalWeights = this.PrecomputeWeights(targetSectionHeight, sourceSectionHeight, this.Sampler);

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

                                // TODO: This is wrong. Adding (int)((startY * heightFactor) - .5) gets close but no cigar.
                                int originY = yw.Index + (int)((startY * heightFactor) - .5);
                                originY = originY.Clamp(0, maxHeight);

                                foreach (Weight xw in horizontalValues)
                                {
                                    if (Math.Abs(xw.Value) < Epsilon)
                                    {
                                        continue;
                                    }

                                    // TODO: This need updating to take into account the target rectangle.
                                    int originX = xw.Index;
                                    originX = originX.Clamp(0, maxWidth);

                                    Bgra sourceColor = source[originX, originY];
                                    r += sourceColor.R * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                    g += sourceColor.G * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                    b += sourceColor.B * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                    a += sourceColor.A * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                }
                            }

                            Bgra destinationColor = new Bgra(b.ToByte(), g.ToByte(), r.ToByte(), a.ToByte());
                            target[x, y] = destinationColor;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Computes the weights to apply at each pixel when resizing.
        /// </summary>
        /// <param name="destinationSize">
        /// The destination section size.
        /// </param>
        /// <param name="sourceSize">
        /// The source section size.
        /// </param>
        /// <param name="sampler">
        /// The <see cref="IResampler"/> containing the resampling algorithm.
        /// </param>
        /// <returns>
        /// The <see cref="T:Weights[]"/>.
        /// </returns>
        private Weights[] PrecomputeWeights(int destinationSize, int sourceSize, IResampler sampler)
        {
            float du = sourceSize / (float)destinationSize;
            float scale = du;

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

        protected struct Weight
        {
            public Weight(int index, double value)
            {
                this.Index = index;
                this.Value = value;
            }

            public readonly int Index;

            public readonly double Value;
        }

        protected class Weights
        {
            public Weights()
            {
                this.Values = new List<Weight>();
            }

            public List<Weight> Values { get; set; }

            public double Sum { get; set; }
        }
    }
}
