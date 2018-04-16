// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct FourByte
    {
        public readonly byte X;

        public readonly byte Y;

        public readonly byte Z;

        public readonly byte W;
    }
}