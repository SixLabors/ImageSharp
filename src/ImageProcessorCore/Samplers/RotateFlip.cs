// <copyright file="RotateFlip.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Samplers
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the rotation and flipping of an image around its center point.
    /// </summary>
    public class RotateFlip : ParallelImageProcessorCore
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RotateFlip"/> class.
        /// </summary>
        /// <param name="rotateType">The <see cref="RotateType"/> used to perform rotation.</param>
        /// <param name="flipType">The <see cref="FlipType"/> used to perform flipping.</param>
        public RotateFlip(RotateType rotateType, FlipType flipType)
        {
            this.RotateType = rotateType;
            this.FlipType = flipType;
        }

        /// <summary>
        /// Gets the <see cref="FlipType"/> used to perform flipping.
        /// </summary>
        public FlipType FlipType { get; }

        /// <summary>
        /// Gets the <see cref="RotateType"/> used to perform rotation.
        /// </summary>
        public RotateType RotateType { get; }

        /// <inheritdoc/>
        public override int Parallelism { get; set; } = 1;

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            switch (this.RotateType)
            {
                case RotateType.Rotate90:
                    Rotate90(target, source);
                    break;
                case RotateType.Rotate180:
                    Rotate180(target, source);
                    break;
                case RotateType.Rotate270:
                    Rotate270(target, source);
                    break;
                default:
                    target.ClonePixels(target.Width, target.Height, source.Pixels);
                    break;
            }

            switch (this.FlipType)
            {
                // No default needed as we have already set the pixels.
                case FlipType.Vertical:
                    FlipX(target);
                    break;
                case FlipType.Horizontal:
                    FlipY(target);
                    break;
            }
        }

        /// <summary>
        /// Rotates the image 270 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="target">The target image.</param>
        /// <param name="source">The source image.</param>
        private void Rotate270(ImageBase target, ImageBase source)
        {
            int width = source.Width;
            int height = source.Height;
            Image temp = new Image(height, width);

            Parallel.For(0, height,
                y =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        int newX = height - y - 1;
                        newX = height - newX - 1;
                        int newY = width - x - 1;
                        newY = width - newY - 1;
                        temp[newX, newY] = source[x, y];
                    }
                    this.OnRowProcessed();
                });

            target.SetPixels(height, width, temp.Pixels);
        }

        /// <summary>
        /// Rotates the image 180 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="target">The target image.</param>
        /// <param name="source">The source image.</param>
        private void Rotate180(ImageBase target, ImageBase source)
        {
            int width = source.Width;
            int height = source.Height;

            Parallel.For(0, height,
                y =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        int newX = width - x - 1;
                        int newY = height - y - 1;
                        target[newX, newY] = source[x, y];
                    }
                    this.OnRowProcessed();
                });
        }

        /// <summary>
        /// Rotates the image 90 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="target">The target image.</param>
        /// <param name="source">The source image.</param>
        private void Rotate90(ImageBase target, ImageBase source)
        {
            int width = source.Width;
            int height = source.Height;
            Image temp = new Image(height, width);

            Parallel.For(0, height,
                y =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        int newX = height - y - 1;
                        temp[newX, x] = source[x, y];
                    }
                    this.OnRowProcessed();
                });

            target.SetPixels(height, width, temp.Pixels);
        }

        /// <summary>
        /// Swaps the image at the X-axis, which goes horizontally through the middle
        /// at half the height of the image.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        private void FlipX(ImageBase target)
        {
            int width = target.Width;
            int height = target.Height;
            int halfHeight = (int)Math.Ceiling(target.Height * .5);
            ImageBase temp = new Image(width, height);
            temp.ClonePixels(width, height, target.Pixels);

            Parallel.For(0, halfHeight,
                y =>
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int newY = height - y - 1;
                            target[x, y] = temp[x, newY];
                            target[x, newY] = temp[x, y];
                        }
                        this.OnRowProcessed();
                    });
        }

        /// <summary>
        /// Swaps the image at the Y-axis, which goes vertically through the middle
        /// at half of the width of the image.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        private void FlipY(ImageBase target)
        {
            int width = target.Width;
            int height = target.Height;
            int halfWidth = (int)Math.Ceiling(width / 2d);
            ImageBase temp = new Image(width, height);
            temp.ClonePixels(width, height, target.Pixels);

            Parallel.For(0, height,
                y =>
                {
                    for (int x = 0; x < halfWidth; x++)
                    {
                        int newX = width - x - 1;
                        target[x, y] = temp[newX, y];
                        target[newX, y] = temp[x, y];
                    }
                    this.OnRowProcessed();
                });
        }
    }
}
