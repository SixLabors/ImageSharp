// <copyright file="BlackWhiteProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image to their black and white equivalent.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class BlackWhiteProcessor<T, TP> : ColorMatrixFilter<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 1.5f,
            M12 = 1.5f,
            M13 = 1.5f,
            M21 = 1.5f,
            M22 = 1.5f,
            M23 = 1.5f,
            M31 = 1.5f,
            M32 = 1.5f,
            M33 = 1.5f,
            M41 = -1f,
            M42 = -1f,
            M43 = -1f,
        };
    }
}
