// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// A <see langword="struct"/> implementing the row processing logic for <see cref="PixelRowDelegateProcessor"/>.
    /// </summary>
    internal readonly struct PixelRowDelegate : IPixelRowDelegate
    {
        private readonly PixelRowOperation pixelRowOperation;

        [MethodImpl(InliningOptions.ShortMethod)]
        public PixelRowDelegate(PixelRowOperation pixelRowOperation)
        {
            this.pixelRowOperation = pixelRowOperation;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(Span<Vector4> span) => this.pixelRowOperation(span);
    }
}
