// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Gif;

internal static class GifThrowHelper
{
    [DoesNotReturn]
    public static void ThrowInvalidImageContentException(string errorMessage)
        => throw new InvalidImageContentException(errorMessage);

    [DoesNotReturn]
    public static void ThrowNoHeader() => throw new InvalidImageContentException("Gif image does not contain a Logical Screen Descriptor.");

    [DoesNotReturn]
    public static void ThrowNoData() => throw new InvalidImageContentException("Unable to read Gif image data");
}
