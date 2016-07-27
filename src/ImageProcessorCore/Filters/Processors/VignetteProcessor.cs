// <copyright file="VignetteProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Creates a vignette effect on the image
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class VignetteProcessor<T, TP> : ImageProcessor<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VignetteProcessor"/> class.
        /// </summary>
        public VignetteProcessor()
        {
            this.VignetteColor.PackVector(Color.Black.ToVector4());
        }

        /// <summary>
        /// Gets or sets the vignette color to apply.
        /// </summary>
        public T VignetteColor { get; set; }

        /// <summary>
        /// Gets or sets the the x-radius.
        /// </summary>
        public float RadiusX { get; set; }

        /// <summary>
        /// Gets or sets the the y-radius.
        /// </summary>
        public float RadiusY { get; set; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            T vignetteColor = this.VignetteColor;
            Vector2 centre = Rectangle.Center(targetRectangle).ToVector2();
            float rX = this.RadiusX > 0 ? this.RadiusX : targetRectangle.Width / 2f;
            float rY = this.RadiusY > 0 ? this.RadiusY : targetRectangle.Height / 2f;
            float maxDistance = (float)Math.Sqrt(rX * rX + rY * rY);

            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            {
                Parallel.For(
                    startY,
                    endY,
                    y =>
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                float distance = Vector2.Distance(centre, new Vector2(x, y));
                                Vector4 sourceColor = sourcePixels[x, y].ToVector4();
                                Vector4 result = Vector4.Lerp(vignetteColor.ToVector4(), sourceColor, 1 - .9f * (distance / maxDistance));
                                T packed = default(T);
                                packed.PackVector(result);
                                targetPixels[x, y] = packed;

                            }
                            this.OnRowProcessed();
                        });
            }
        }
    }
}

