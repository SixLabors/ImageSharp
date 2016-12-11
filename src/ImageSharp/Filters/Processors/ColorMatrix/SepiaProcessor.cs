// <copyright file="SepiaProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processors
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image to their sepia equivalent.
    /// The formula used matches the svg specification. <see href="http://www.w3.org/TR/filter-effects/#sepiaEquivalent"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class SepiaProcessor<TColor, TPacked> : ColorMatrixFilter<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4
        {
            M11 = .393F,
            M12 = .349F,
            M13 = .272F,
            M21 = .769F,
            M22 = .686F,
            M23 = .534F,
            M31 = .189F,
            M32 = .168F,
            M33 = .131F,
            M44 = 1
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}