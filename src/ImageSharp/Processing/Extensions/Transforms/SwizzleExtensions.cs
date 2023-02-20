// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// Defines extensions that allow the application of swizzle operations on an <see cref="Image"/>
/// </summary>
public static class SwizzleExtensions
{
    /// <summary>
    /// Swizzles an image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="swizzler">The swizzler function.</param>
    /// <typeparam name="TSwizzler">The swizzler function type.</typeparam>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext Swizzle<TSwizzler>(this IImageProcessingContext source, TSwizzler swizzler)
        where TSwizzler : struct, ISwizzler
        => source.ApplyProcessor(new SwizzleProcessor<TSwizzler>(swizzler));
}
