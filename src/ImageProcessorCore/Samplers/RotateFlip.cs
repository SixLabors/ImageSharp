// <copyright file="RotateFlip.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Rotates and flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="rotateType">The <see cref="RotateType"/> to perform the rotation.</param>
        /// <param name="flipType">The <see cref="FlipType"/> to perform the flip.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<T, TP> RotateFlip<T, TP>(this Image<T, TP> source, RotateType rotateType, FlipType flipType, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            RotateFlipProcessor<T, TP> processor = new RotateFlipProcessor<T, TP>(rotateType, flipType);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(source.Width, source.Height, source.Bounds, source.Bounds, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }
    }
}
