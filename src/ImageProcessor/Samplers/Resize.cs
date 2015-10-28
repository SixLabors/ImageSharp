// <copyright file="Resize.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Threading.Tasks;

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
        /// The horizontal weights.
        /// </summary>
        private Weights[] horizontalWeights;

        /// <summary>
        /// The vertical weights.
        /// </summary>
        private Weights[] verticalWeights;

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

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <inheritdoc/>
        protected override void OnApply(Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            this.horizontalWeights = this.PrecomputeWeights(targetRectangle.Width, sourceRectangle.Width);
            this.verticalWeights = this.PrecomputeWeights(targetRectangle.Height, sourceRectangle.Height);
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int targetY = targetRectangle.Y;
            int targetBottom = targetRectangle.Bottom;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;
            //Vector<int> endVX = new Vector<int>(targetRectangle.Right);

            Parallel.For(
                startY,
                endY,
                y =>
                {
                    if (y >= targetY && y < targetBottom)
                    {
                        List<Weight> verticalValues = this.verticalWeights[y].Values;
                        double verticalSum = this.verticalWeights[y].Sum;

                        for (int x = startX; x < endX; x++)
                        {
                            List<Weight> horizontalValues = this.horizontalWeights[x].Values;
                            double horizontalSum = this.horizontalWeights[x].Sum;

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

                                int originY = yw.Index;

                                foreach (Weight xw in horizontalValues)
                                {
                                    if (Math.Abs(xw.Value) < Epsilon)
                                    {
                                        continue;
                                    }

                                    int originX = xw.Index;
                                    ColorVector sourceColor = source[originX, originY];
                                    //sourceColor = PixelOperations.ToLinear(sourceColor);

                                    r += sourceColor.R * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                    g += sourceColor.G * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                    b += sourceColor.B * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                    a += sourceColor.A * (yw.Value / verticalSum) * (xw.Value / horizontalSum);
                                }
                            }

                            // TODO: Double cast?
                            Bgra destinationColor = new ColorVector(b, g, r, a);//PixelOperations.ToSrgb(new ColorVector(b, g, r, a));
                            target[x, y] = destinationColor;
                        }
                    }
                });
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

            Parallel.For(
                0,
                destinationSize,
                i =>
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
                    });

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
