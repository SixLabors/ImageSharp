// <copyright file="Resize.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the resizing of images using various algorithms.
    /// </summary>
    public class Resize : Resampler
    {
        /// <summary>
        /// The image used for storing the first pass pixels.
        /// </summary>
        private Image firstPass;

        /// <summary>
        /// Initializes a new instance of the <see cref="Resize"/> class.
        /// </summary>
        /// <param name="sampler">
        /// The sampler to perform the resize operation.
        /// </param>
        public Resize(IResampler sampler)
            : base(sampler)
        {
        }

        /// <inheritdoc/>
        public override int Parallelism { get; set; } = 1;

        /// <inheritdoc/>
        protected override void OnApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (!(this.Sampler is NearestNeighborResampler))
            {
                this.HorizontalWeights = this.PrecomputeWeights(targetRectangle.Width, sourceRectangle.Width);
                this.VerticalWeights = this.PrecomputeWeights(targetRectangle.Height, sourceRectangle.Height);
            }

            this.firstPass = new Image(target.Width, source.Height);
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            // Jump out, we'll deal with that later.
            if (source.Bounds == target.Bounds && sourceRectangle == targetRectangle)
            {
                return;
            }

            int width = target.Width;
            int height = target.Height;
            int sourceHeight = sourceRectangle.Height;
            int targetX = target.Bounds.X;
            int targetY = target.Bounds.Y;
            int targetRight = target.Bounds.Right;
            int targetBottom = target.Bounds.Bottom;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;
            bool compand = this.Compand;

            if (this.Sampler is NearestNeighborResampler)
            {
                // Scaling factors
                float widthFactor = sourceRectangle.Width / (float)targetRectangle.Width;
                float heightFactor = sourceRectangle.Height / (float)targetRectangle.Height;

                Parallel.For(
                    startY,
                    endY,
                    y =>
                    {
                        if (targetY <= y && y < targetBottom)
                        {
                            // Y coordinates of source points
                            int originY = (int)((y - startY) * heightFactor);

                            for (int x = startX; x < endX; x++)
                            {
                                if (targetX <= x && x < targetRight)
                                {
                                    // X coordinates of source points
                                    int originX = (int)((x - startX) * widthFactor);

                                    target[x, y] = source[originX, originY];
                                }
                            }

                            this.OnRowProcessed();
                        }
                    });

                // Break out now.
                return;
            }

            // Interpolate the image using the calculated weights.
            // A 2-pass 1D algorithm appears to be faster than splitting a 1-pass 2D algorithm 
            // First process the columns. Since we are not using multiple threads startY and endY
            // are the upper and lower bounds of the source rectangle.
            Parallel.For(
                0,
                sourceHeight,
                y =>
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            if (x >= 0 && x < width)
                            {
                                // Ensure offsets are normalised for cropping and padding.
                                int offsetX = x - startX;
                                float sum = this.HorizontalWeights[offsetX].Sum;
                                Weight[] horizontalValues = this.HorizontalWeights[offsetX].Values;

                                // Destination color components
                                Color destination = new Color();

                                for (int i = 0; i < sum; i++)
                                {
                                    Weight xw = horizontalValues[i];
                                    int originX = xw.Index;
                                    Color sourceColor = compand ? Color.Expand(source[originX, y]) : source[originX, y];
                                    destination += sourceColor * xw.Value;
                                }

                                if (compand)
                                {
                                    destination = Color.Compress(destination);
                                }

                                this.firstPass[x, y] = destination;
                            }
                        }
                    });

            // Now process the rows.
            Parallel.For(
                startY,
                endY,
                y =>
                    {
                        if (y >= 0 && y < height)
                        {
                            // Ensure offsets are normalised for cropping and padding.
                            int offsetY = y - startY;
                            float sum = this.VerticalWeights[offsetY].Sum;
                            Weight[] verticalValues = this.VerticalWeights[offsetY].Values;

                            for (int x = 0; x < width; x++)
                            {
                                // Destination color components
                                Color destination = new Color();

                                for (int i = 0; i < sum; i++)
                                {
                                    Weight yw = verticalValues[i];
                                    int originY = yw.Index;
                                    Color sourceColor = compand ? Color.Expand(this.firstPass[x, originY]) : this.firstPass[x, originY];
                                    destination += sourceColor * yw.Value;
                                }

                                if (compand)
                                {
                                    destination = Color.Compress(destination);
                                }

                                target[x, y] = destination;
                            }
                        }

                        this.OnRowProcessed();
                    });
        }

        /// <inheritdoc/>
        protected override void AfterApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            // Copy the pixels over.
            if (source.Bounds == target.Bounds && sourceRectangle == targetRectangle)
            {
                target.ClonePixels(target.Width, target.Height, source.Pixels);
            }

            // Clean up
            this.firstPass?.Dispose();
        }
    }
}