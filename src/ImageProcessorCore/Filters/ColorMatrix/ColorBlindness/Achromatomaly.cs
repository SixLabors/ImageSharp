// <copyright file="Achromatomaly.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Achromatomaly (Color desensitivity) color blindness.
    /// </summary>
    public class Achromatomaly : ColorMatrixFilter
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = .618f,
            M12 = .163f,
            M13 = .163f,
            M21 = .320f,
            M22 = .775f,
            M23 = .320f,
            M31 = .062f,
            M32 = .062f,
            M33 = .516f
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
