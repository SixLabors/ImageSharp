// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// A visitor to implement double-dispatch pattern in order to apply pixel-specific operations
    /// on non-generic <see cref="Image"/> instances. The operation is dispatched by <see cref="Image.AcceptVisitor"/>.
    /// </summary>
    internal interface IImageVisitor
    {
        /// <summary>
        /// Provides a pixel-specific implementation for a given operation.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        void Visit<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>;
    }
}