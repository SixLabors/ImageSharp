// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// An <see langword="interface"/> used by the row delegates for a given <see cref="PixelRowDelegateProcessor{TPixel,TDelegate}"/> instance
    /// </summary>
    public interface IPixelRowDelegate
    {
        /// <summary>
        /// Applies the current pixel row delegate to a target row of preprocessed pixels.
        /// </summary>
        /// <param name="span">The target row of <see cref="Vector4"/> pixels to process.</param>
        /// <param name="offset">The initial horizontal and vertical offset for the input pixels to process.</param>
        void Invoke(Span<Vector4> span, Point offset);
    }
}
