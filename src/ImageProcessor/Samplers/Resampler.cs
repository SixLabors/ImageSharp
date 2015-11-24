// <copyright file="Resampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    using System;
    using System.Collections.Immutable;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the resampling of images using various algorithms.
    /// </summary>
    public class Resampler : ParallelImageProcessor
    {
        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// The angle of rotation.
        /// </summary>
        private double angle;

        /// <summary>
        /// The horizontal weights.
        /// </summary>
        private Weights[] horizontalWeights;

        /// <summary>
        /// The vertical weights.
        /// </summary>
        private Weights[] verticalWeights;

        /// <summary>
        /// Initializes a new instance of the <see cref="Resampler"/> class.
        /// </summary>
        /// <param name="sampler">
        /// The sampler to perform the resize operation.
        /// </param>
        public Resampler(IResampler sampler)
        {
            Guard.NotNull(sampler, nameof(sampler));

            this.Sampler = sampler;
        }

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <summary>
        /// Gets or sets the angle of rotation.
        /// </summary>
        public double Angle
        {
            get
            {
                return this.angle;
            }

            set
            {
                if (value > 360)
                {
                    value -= 360;
                }

                if (value < 0)
                {
                    value += 360;
                }

                this.angle = value;
            }
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle)
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
            Point centre = Rectangle.Center(sourceRectangle);
            bool rotate = this.angle > 0 && this.angle < 360;

            Parallel.For(
                startY,
                endY,
                y =>
                {
                    if (y >= targetY && y < targetBottom)
                    {
                        ImmutableArray<Weight> verticalValues = this.verticalWeights[y].Values;
                        float verticalSum = this.verticalWeights[y].Sum;

                        for (int x = startX; x < endX; x++)
                        {
                            ImmutableArray<Weight> horizontalValues = this.horizontalWeights[x].Values;
                            float horizontalSum = this.horizontalWeights[x].Sum;

                            // Destination color components
                            Color destination = new Color(0, 0, 0, 0);

                            foreach (Weight yw in verticalValues)
                            {
                                int originY = yw.Index;

                                foreach (Weight xw in horizontalValues)
                                {
                                    int originX = xw.Index;
                                    Color sourceColor;

                                    float weight;

                                    if (rotate)
                                    {
                                        // Rotating at the centre point
                                        Point rotated = ImageMaths.RotatePoint(new Point(originX, originY), this.angle, centre);
                                        originX = rotated.X;
                                        originY = rotated.Y;

                                        // TODO: This can't work. We're not normalising properly since weights are skipped.
                                        // Also... This is so slow!
                                        if (sourceRectangle.Contains(originX, originY))
                                        {
                                            sourceColor = Color.InverseCompand(source[originX, originY]);
                                            weight = (yw.Value / verticalSum) * (xw.Value / horizontalSum);

                                            destination.R += sourceColor.R * weight;
                                            destination.G += sourceColor.G * weight;
                                            destination.B += sourceColor.B * weight;
                                            destination.A += sourceColor.A * weight;
                                        }
                                    }

                                    sourceColor = Color.InverseCompand(source[originX, originY]);
                                    weight = (yw.Value / verticalSum) * (xw.Value / horizontalSum);

                                    destination.R += sourceColor.R * weight;
                                    destination.G += sourceColor.G * weight;
                                    destination.B += sourceColor.B * weight;
                                    destination.A += sourceColor.A * weight;
                                }
                            }

                            destination = Color.Compand(destination);

                            // Ensure are alpha values only reflect possible values to prevent bleed.
                            destination.A = (float)Math.Round(destination.A, 2);

                            target[x, y] = destination;
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
            float du = sourceSize / (float)destinationSize;
            float scale = du;

            if (scale < 1)
            {
                scale = 1;
            }

            float ru = (float)Math.Ceiling(scale * sampler.Radius);
            Weights[] result = new Weights[destinationSize];

            Parallel.For(
                0,
                destinationSize,
                i =>
                    {
                        float fu = ((i + .5f) * du) - 0.5f;
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

                        float sum = 0;
                        result[i] = new Weights();

                        ImmutableArray<Weight>.Builder builder = ImmutableArray.CreateBuilder<Weight>();
                        for (int a = startU; a <= endU; a++)
                        {
                            float w = sampler.GetValue((a - fu) / scale);

                            if (Math.Abs(w) > Epsilon)
                            {
                                sum += w;
                                builder.Add(new Weight(a, w));
                            }
                        }

                        result[i].Values = builder.ToImmutable();
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
            public readonly float Value;

            /// <summary>
            /// Initializes a new instance of the <see cref="Weight"/> struct.
            /// </summary>
            /// <param name="index">The index.</param>
            /// <param name="value">The value.</param>
            public Weight(int index, float value)
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
            /// Gets or sets the values.
            /// </summary>
            public ImmutableArray<Weight> Values { get; set; }

            /// <summary>
            /// Gets or sets the sum.
            /// </summary>
            public float Sum { get; set; }
        }
    }
}
