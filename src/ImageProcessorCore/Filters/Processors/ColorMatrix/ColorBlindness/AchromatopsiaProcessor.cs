// <copyright file="AchromatopsiaProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Achromatopsia (Monochrome) color blindness.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class AchromatopsiaProcessor<T, TP> : ColorMatrixFilter<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = .299f,
            M12 = .299f,
            M13 = .299f,
            M21 = .587f,
            M22 = .587f,
            M23 = .587f,
            M31 = .114f,
            M32 = .114f,
            M33 = .114f
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
