// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// A visitor to implement a double-dispatch pattern in order to apply pixel-specific operations
    /// on non-generic <see cref="Image"/> instances.
    /// </summary>
    public interface IImageVisitor
    {
        /// <summary>
        /// Provides a pixel-specific implementation for a given operation.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        void Visit<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>;
    }

    /// <summary>
    /// A visitor to implement a double-dispatch pattern in order to apply pixel-specific operations
    /// on non-generic <see cref="Image"/> instances.
    /// </summary>
    public interface IImageVisitorAsync
    {
        /// <summary>
        /// Provides a pixel-specific implementation for a given operation.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task VisitAsync<TPixel>(Image<TPixel> image, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>;
    }
}
