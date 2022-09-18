// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Gif;

internal static class GifThrowHelper
{
    public static void ThrowInvalidImageContentException(string errorMessage)
        => throw new InvalidImageContentException(errorMessage);

    public static void ThrowInvalidImageContentException(string errorMessage, Exception innerException) => throw new InvalidImageContentException(errorMessage, innerException);
}
