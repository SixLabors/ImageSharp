// <copyright file="ContrastProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor{T,TP}"/> to change the contrast of an <see cref="Image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class ContrastProcessor<T, TP> : ImageProcessor<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContrastProcessor"/> class.
        /// </summary>
        /// <param name="contrast">The new contrast of the image. Must be between -100 and 100.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="contrast"/> is less than -100 or is greater than 100.
        /// </exception>
        public ContrastProcessor(int contrast)
        {
            Guard.MustBeBetweenOrEqualTo(contrast, -100, 100, nameof(contrast));
            this.Value = contrast;
        }

        /// <summary>
        /// Gets the contrast value.
        /// </summary>
        public int Value { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            float contrast = (100f + this.Value) / 100f;
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Vector4 contrastVector = new Vector4(contrast, contrast, contrast, 1);
            Vector4 shiftVector = new Vector4(.5f, .5f, .5f, 1);

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
                                    Vector4 vector = (sourcePixels[x, y]).ToVector4().Expand();
                                    vector -= shiftVector;
                                    vector *= contrastVector;
                                    vector += shiftVector;
                                    T packed = default(T);
                                    packed.PackVector(vector.Compress());
                                    targetPixels[x, y] = packed;
                                }
                                this.OnRowProcessed();
                            }
                        });
            }
        }
    }
}
