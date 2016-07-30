// <copyright file="RotateFlipProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the rotation and flipping of an image around its center point.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class RotateFlipProcessor<T, TP> : ImageSampler<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RotateFlipProcessor"/> class.
        /// </summary>
        /// <param name="rotateType">The <see cref="RotateType"/> used to perform rotation.</param>
        /// <param name="flipType">The <see cref="FlipType"/> used to perform flipping.</param>
        public RotateFlipProcessor(RotateType rotateType, FlipType flipType)
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
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            switch (this.RotateType)
            {
                case RotateType.Rotate90:
                    this.Rotate90(target, source);
                    break;
                case RotateType.Rotate180:
                    this.Rotate180(target, source);
                    break;
                case RotateType.Rotate270:
                    this.Rotate270(target, source);
                    break;
                default:
                    target.ClonePixels(target.Width, target.Height, source.Pixels);
                    break;
            }

            switch (this.FlipType)
            {
                // No default needed as we have already set the pixels.
                case FlipType.Vertical:
                    this.FlipX(target);
                    break;
                case FlipType.Horizontal:
                    this.FlipY(target);
                    break;
            }
        }

        /// <summary>
        /// Rotates the image 270 degrees clockwise at the centre point.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="target">The target image.</param>
        /// <param name="source">The source image.</param>
        private void Rotate270(ImageBase<T, TP> target, ImageBase<T, TP> source)
        {
            int width = source.Width;
            int height = source.Height;
            Image<T, TP> temp = new Image<T, TP>(height, width);

            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> tempPixels = temp.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    Bootstrapper.Instance.ParallelOptions,
                    y =>
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int newX = height - y - 1;
                                newX = height - newX - 1;
                                int newY = width - x - 1;
                                newY = width - newY - 1;
                                tempPixels[newX, newY] = sourcePixels[x, y];
                            }

                            this.OnRowProcessed();
                        });
            }

            target.SetPixels(height, width, temp.Pixels);
        }

        /// <summary>
        /// Rotates the image 180 degrees clockwise at the centre point.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="target">The target image.</param>
        /// <param name="source">The source image.</param>
        private void Rotate180(ImageBase<T, TP> target, ImageBase<T, TP> source)
        {
            int width = source.Width;
            int height = source.Height;

            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    Bootstrapper.Instance.ParallelOptions,
                    y =>
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int newX = width - x - 1;
                                int newY = height - y - 1;
                                targetPixels[newX, newY] = sourcePixels[x, y];
                            }

                            this.OnRowProcessed();
                        });
            }
        }

        /// <summary>
        /// Rotates the image 90 degrees clockwise at the centre point.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="target">The target image.</param>
        /// <param name="source">The source image.</param>
        private void Rotate90(ImageBase<T, TP> target, ImageBase<T, TP> source)
        {
            int width = source.Width;
            int height = source.Height;
            Image<T, TP> temp = new Image<T, TP>(height, width);

            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> tempPixels = temp.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    Bootstrapper.Instance.ParallelOptions,
                    y =>
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int newX = height - y - 1;
                                tempPixels[newX, x] = sourcePixels[x, y];
                            }

                            this.OnRowProcessed();
                        });
            }

            target.SetPixels(height, width, temp.Pixels);
        }

        /// <summary>
        /// Swaps the image at the X-axis, which goes horizontally through the middle
        /// at half the height of the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="target">Target image to apply the process to.</param>
        private void FlipX(ImageBase<T, TP> target)
        {
            int width = target.Width;
            int height = target.Height;
            int halfHeight = (int)Math.Ceiling(target.Height * .5);
            Image<T, TP> temp = new Image<T, TP>(width, height);
            temp.ClonePixels(width, height, target.Pixels);

            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            using (IPixelAccessor<T, TP> tempPixels = temp.Lock())
            {
                Parallel.For(
                    0,
                    halfHeight,
                    Bootstrapper.Instance.ParallelOptions,
                    y =>
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int newY = height - y - 1;
                                targetPixels[x, y] = tempPixels[x, newY];
                                targetPixels[x, newY] = tempPixels[x, y];
                            }

                            this.OnRowProcessed();
                        });
            }
        }

        /// <summary>
        /// Swaps the image at the Y-axis, which goes vertically through the middle
        /// at half of the width of the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="target">Target image to apply the process to.</param>
        private void FlipY(ImageBase<T, TP> target)
        {
            int width = target.Width;
            int height = target.Height;
            int halfWidth = (int)Math.Ceiling(width / 2d);
            Image<T, TP> temp = new Image<T, TP>(width, height);
            temp.ClonePixels(width, height, target.Pixels);

            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            using (IPixelAccessor<T, TP> tempPixels = temp.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    Bootstrapper.Instance.ParallelOptions,
                    y =>
                        {
                            for (int x = 0; x < halfWidth; x++)
                            {
                                int newX = width - x - 1;
                                targetPixels[x, y] = tempPixels[newX, y];
                                targetPixels[newX, y] = tempPixels[x, y];
                            }

                            this.OnRowProcessed();
                        });
            }
        }
    }
}
