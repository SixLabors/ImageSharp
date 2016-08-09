// <copyright file="BinaryThresholdProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor{T,TP}"/> to perform binary threshold filtering against an 
    /// <see cref="Image"/>. The image will be converted to Grayscale before thresholding 
    /// occurs.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class BinaryThresholdProcessor<T, TP> : ImageProcessor<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThresholdProcessor"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="threshold"/> is less than 0 or is greater than 1.
        /// </exception>
        public BinaryThresholdProcessor(float threshold)
        {
            // TODO: Check limit.
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Value = threshold;

            T upper = default(T);
            upper.PackFromVector4(Color.White.ToVector4());
            this.UpperColor = upper;

            T lower = default(T);
            lower.PackFromVector4(Color.Black.ToVector4());
            this.LowerColor = lower;
        }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Value { get; }

        /// <summary>
        /// The color to use for pixels that are above the threshold.
        /// </summary>
        public T UpperColor { get; set; }

        /// <summary>
        /// The color to use for pixels that fall below the threshold.
        /// </summary>
        public T LowerColor { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            new GrayscaleBt709Processor<T, TP>().Apply(source, source, sourceRectangle);
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            // target.SetPixels(source.Width, source.Height, source.Pixels);


            float threshold = this.Value;
            T upper = this.UpperColor;
            T lower = this.LowerColor;
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            {
                Parallel.For(
                startY,
                endY,
                this.ParallelOptions,
                y =>
                    {
                        if (y >= sourceY && y < sourceBottom)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                T color = sourcePixels[x, y];

                                // Any channel will do since it's Grayscale.
                                targetPixels[x, y] = color.ToVector4().X >= threshold ? upper : lower;
                            }
                            this.OnRowProcessed();
                        }
                    });
            }
        }
    }
}
