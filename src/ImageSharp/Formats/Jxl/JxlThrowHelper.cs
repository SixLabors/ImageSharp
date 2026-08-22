// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Jxl;

internal static class JxlThrowHelper
{
    [DoesNotReturn]
    public static void ThrowEndOfStream() => throw new EndOfStreamException();
}
