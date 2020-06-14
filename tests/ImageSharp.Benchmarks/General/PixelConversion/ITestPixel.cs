// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    public interface ITestPixel<T>
        where T : struct, ITestPixel<T>
    {
        void FromRgba32(Rgba32 source);

        void FromRgba32(ref Rgba32 source);

        void FromBytes(byte r, byte g, byte b, byte a);

        void FromVector4(Vector4 source);

        void FromVector4(ref Vector4 source);

        Rgba32 ToRgba32();

        void CopyToRgba32(ref Rgba32 dest);

        Vector4 ToVector4();

        void CopyToVector4(ref Vector4 dest);
    }
}
