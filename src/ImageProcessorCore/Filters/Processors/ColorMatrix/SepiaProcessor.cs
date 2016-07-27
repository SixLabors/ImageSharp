// <copyright file="SepiaProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image to their sepia equivalent.
    /// The formula used matches the svg specification. <see href="http://www.w3.org/TR/filter-effects/#sepiaEquivalent"/>
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class SepiaProcessor<T, TP> : ColorMatrixFilter<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = .393f,
            M12 = .349f,
            M13 = .272f,
            M21 = .769f,
            M22 = .686f,
            M23 = .534f,
            M31 = .189f,
            M32 = .168f,
            M33 = .131f
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
