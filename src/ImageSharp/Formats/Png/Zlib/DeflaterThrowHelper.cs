// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Png.Zlib
{
    internal static class DeflaterThrowHelper
    {
        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowAlreadyFinished() => throw new InvalidOperationException("Finish() already called.");

        [MethodImpl(InliningOptions.ColdPath)]
        public static void ThrowAlreadyClosed() => throw new InvalidOperationException("Deflator already closed.");
    }
}
