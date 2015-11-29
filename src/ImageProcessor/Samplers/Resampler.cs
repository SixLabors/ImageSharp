// <copyright file="Resampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the resampling of images using various algorithms.
    /// </summary>
    public class Resampler : ParallelImageProcessor
    {
        /// <summary>
        /// The angle of rotation.
        /// </summary>
        private float angle;

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
        public float Angle
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
        protected override void OnApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            this.horizontalWeights = this.PrecomputeWeights(targetRectangle.Width, sourceRectangle.Width);
            this.verticalWeights = this.PrecomputeWeights(targetRectangle.Height, sourceRectangle.Height);
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            bool rotate = this.angle > 0 && this.angle < 360;

            // Split the two methods up so we can keep standard resize as performant as possible.
            if (rotate)
            {
                this.ApplyResizeAndRotate(target, source, targetRectangle, sourceRectangle, startY, endY);
            }
            else
            {
                this.ApplyResizeOnly(target, source, targetRectangle, startY, endY);
            }
        }

        /// <summary>
        /// Resamples the specified <see cref="ImageBase"/> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the location and size of the drawn image.
        /// The image is scaled to fit the rectangle.
        /// </param>
        /// <param name="startY">The index of the row within the source image to start processing.</param>
        /// <param name="endY">The index of the row within the source image to end processing.</param>
        /// <remarks>
        /// The method keeps the source image unchanged and returns the
        /// the result of image process as new image.
        /// </remarks>
        private void ApplyResizeOnly(ImageBase target, ImageBase source, Rectangle targetRectangle, int startY, int endY)
        {
            int targetY = targetRectangle.Y;
            int targetBottom = targetRectangle.Bottom;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;

            Parallel.For(
                startY,
                endY,
                y =>
                {
                    if (y >= targetY && y < targetBottom)
                    {
                        Weight[] verticalValues = this.verticalWeights[y].Values;

                        for (int x = startX; x < endX; x++)
                        {
                            Weight[] horizontalValues = this.horizontalWeights[x].Values;

                            // Destination color components
                            Color destination = new Color(0, 0, 0, 0);

                            foreach (Weight yw in verticalValues)
                            {
                                int originY = yw.Index;

                                foreach (Weight xw in horizontalValues)
                                {
                                    int originX = xw.Index;
                                    Color sourceColor = Color.InverseCompand(source[originX, originY]);
                                    float weight = yw.Value * xw.Value;

                                    destination.R += sourceColor.R * weight;
                                    destination.G += sourceColor.G * weight;
                                    destination.B += sourceColor.B * weight;
                                    destination.A += sourceColor.A * weight;
                                }
                            }

                            destination = Color.Compand(destination);

                            // Round alpha values in an attempt to prevent bleed.
                            destination.A = (float)Math.Round(destination.A, 2);

                            target[x, y] = destination;
                        }
                    }
                });
        }

        /// <summary>
        /// Resamples and rotates the specified <see cref="ImageBase"/> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the location and size of the drawn image.
        /// The image is scaled to fit the rectangle.
        /// </param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="startY">The index of the row within the source image to start processing.</param>
        /// <param name="endY">The index of the row within the source image to end processing.</param>
        /// <remarks>
        /// The method keeps the source image unchanged and returns the
        /// the result of image process as new image.
        /// </remarks>
        private void ApplyResizeAndRotate(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int targetY = targetRectangle.Y;
            int targetBottom = targetRectangle.Bottom;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;
            float negativeAngle = -this.angle;
            Vector2 centre = Rectangle.Center(sourceRectangle);

            Parallel.For(
                startY,
                endY,
                y =>
                {
                    if (y >= targetY && y < targetBottom)
                    {
                        Weight[] verticalValues = this.verticalWeights[y].Values;

                        for (int x = startX; x < endX; x++)
                        {
                            Weight[] horizontalValues = this.horizontalWeights[x].Values;

                            // Destination color components
                            Color destination = new Color(0, 0, 0, 0);

                            foreach (Weight yw in verticalValues)
                            {
                                int originY = yw.Index;

                                foreach (Weight xw in horizontalValues)
                                {
                                    int originX = xw.Index;

                                    // Rotate at the centre point
                                    Vector2 rotated = ImageMaths.RotatePoint(new Vector2(originX, originY), centre, negativeAngle);
                                    int rotatedX = (int)rotated.X;
                                    int rotatedY = (int)rotated.Y;

                                    if (sourceRectangle.Contains(rotatedX, rotatedY))
                                    {
                                        Color sourceColor = Color.InverseCompand(source[rotatedX, rotatedY]);
                                        float weight = yw.Value * xw.Value;
                                        destination.R += sourceColor.R * weight;
                                        destination.G += sourceColor.G * weight;
                                        destination.B += sourceColor.B * weight;
                                        destination.A += sourceColor.A * weight;
                                    }
                                    else
                                    {
                                        // This is well hacky but clears up most of the 
                                        // Alpha bleeding issues present in rotated images.
                                        float weight = yw.Value * xw.Value;
                                        destination.A += .9f * weight;
                                    }
                                }
                            }

                            destination = Color.Compand(destination);

                            // Round alpha values in an attempt to prevent bleed.
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

                        List<Weight> builder = new List<Weight>();
                        for (int a = startU; a <= endU; a++)
                        {
                            float w = sampler.GetValue((a - fu) / scale);

                            if (w < 0 || w > 0)
                            {
                                sum += w;
                                builder.Add(new Weight(a, w));
                            }
                        }

                        // Normalise the values
                        if (Math.Abs(sum) > 0.00001f)
                        {
                            builder.ForEach(w => w.Value /= sum);
                        }

                        result[i].Values = builder.ToArray();
                        result[i].Sum = sum;
                    });

            return result;
        }

        /// <summary>
        /// Represents the weight to be added to a scaled pixel.
        /// </summary>
        protected class Weight
        {
            /// <summary>
            /// The pixel index.
            /// </summary>
            public readonly int Index;

            /// <summary>
            /// Initializes a new instance of the <see cref="Weight"/> class.
            /// </summary>
            /// <param name="index">The index.</param>
            /// <param name="value">The value.</param>
            public Weight(int index, float value)
            {
                this.Index = index;
                this.Value = value;
            }

            /// <summary>
            /// Gets or sets the result of the interpolation algorithm.
            /// </summary>
            public float Value { get; set; }
        }

        /// <summary>
        /// Represents a collection of weights and their sum.
        /// </summary>
        protected class Weights
        {
            /// <summary>
            /// Gets or sets the values.
            /// </summary>
            public Weight[] Values { get; set; }

            /// <summary>
            /// Gets or sets the sum.
            /// </summary>
            public float Sum { get; set; }
        }
    }
}
