// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// A <see langword="struct"/> implementing the row processing logic for <see cref="PositionAwarePixelRowDelegateProcessor{TDelegate}"/>.
    /// </summary>
    internal readonly struct PositionAwarePixelRowDelegate : IPixelRowDelegate<Point>
    {
        private readonly PixelRowOperation<Point> pixelRowOperation;

        [MethodImpl(InliningOptions.ShortMethod)]
        public PositionAwarePixelRowDelegate(PixelRowOperation<Point> pixelRowOperation)
        {
            this.pixelRowOperation = pixelRowOperation;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(Span<Vector4> span, Point offset) => this.pixelRowOperation(span, offset);
    }
}
