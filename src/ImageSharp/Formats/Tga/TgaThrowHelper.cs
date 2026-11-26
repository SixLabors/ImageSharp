// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Tga;

internal static class TgaThrowHelper
{
    [DoesNotReturn]
    public static void ThrowInvalidImageContentException(string errorMessage)
        => throw new InvalidImageContentException(errorMessage);

    [DoesNotReturn]
    public static void ThrowNotSupportedException(string errorMessage)
        => throw new NotSupportedException(errorMessage);
}
