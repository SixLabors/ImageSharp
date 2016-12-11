// <copyright file="AchromatomalyProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processors
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Achromatomaly (Color desensitivity) color blindness.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class AchromatomalyProcessor<TColor, TPacked> : ColorMatrixFilter<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = .618F,
            M12 = .163F,
            M13 = .163F,
            M21 = .320F,
            M22 = .775F,
            M23 = .320F,
            M31 = .062F,
            M32 = .062F,
            M33 = .516F,
            M44 = 1
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
