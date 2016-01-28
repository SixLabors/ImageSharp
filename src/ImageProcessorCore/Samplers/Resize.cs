// <copyright file="Resize.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Samplers
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
            if (source.Bounds == target.Bounds)
            {
                return;
            }

            int sourceBottom = source.Bounds.Bottom;
            int targetY = targetRectangle.Y;
            int targetBottom = targetRectangle.Bottom;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;

            if (this.Sampler is NearestNeighborResampler)
            {
                // Scaling factors
                float widthFactor = source.Width / (float)target.Width;
                float heightFactor = source.Height / (float)target.Height;

                Parallel.For(
                    startY,
                    endY,
                    y =>
                    {
                        if (y >= targetY && y < targetBottom)
                        {
                            // Y coordinates of source points
                            int originY = (int)((y - targetY) * heightFactor);

                            for (int x = startX; x < endX; x++)
                            {
                                // X coordinates of source points
                                int originX = (int)((x - startX) * widthFactor);

                                target[x, y] = source[originX, originY];
                            }
                            this.OnRowProcessed();
                        }
                    });

                // Break out now.
                return;
            }

            // Interpolate the image using the calculated weights.
            // A 2-pass 1D algorithm appears to be faster than splitting a 1-pass 2D algorithm 
            // First process the columns.
            Parallel.For(
                0,
                sourceBottom,
                y =>
                {
                    for (int x = startX; x < endX; x++)
                    {
                        Weight[] horizontalValues = this.HorizontalWeights[x].Values;

                        // Destination color components
                        Color destination = new Color();

                        foreach (Weight xw in horizontalValues)
                        {
                            int originX = xw.Index;
                            Color sourceColor = Color.Expand(source[originX, y]);
                            destination += sourceColor * xw.Value;
                        }

                        destination = Color.Compress(destination);
                        this.firstPass[x, y] = destination;
                    }
                });

            // Now process the rows.
            Parallel.For(
                startY,
                endY,
                y =>
                {
                    if (y >= targetY && y < targetBottom)
                    {
                        Weight[] verticalValues = this.VerticalWeights[y].Values;

                        for (int x = startX; x < endX; x++)
                        {
                            // Destination color components
                            Color destination = new Color();

                            foreach (Weight yw in verticalValues)
                            {
                                int originY = yw.Index;
                                int originX = x;
                                Color sourceColor = Color.Expand(this.firstPass[originX, originY]);
                                destination += sourceColor * yw.Value;
                            }

                            destination = Color.Compress(destination);
                            target[x, y] = destination;
                        }
                        this.OnRowProcessed();
                    }
                });
        }

        /// <inheritdoc/>
        protected override void AfterApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            // Copy the pixels over.
            if (source.Bounds == target.Bounds)
            {
                target.ClonePixels(target.Width, target.Height, source.Pixels);
            }
        }
    }
}