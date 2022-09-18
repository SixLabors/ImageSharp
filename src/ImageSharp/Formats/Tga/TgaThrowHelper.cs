// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tga;

internal static class TgaThrowHelper
{
    public static void ThrowInvalidImageContentException(string errorMessage)
        => throw new InvalidImageContentException(errorMessage);

    public static void ThrowInvalidImageContentException(string errorMessage, Exception innerException)
        => throw new InvalidImageContentException(errorMessage, innerException);

    public static void ThrowNotSupportedException(string errorMessage)
        => throw new NotSupportedException(errorMessage);
}
