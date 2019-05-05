// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    public struct Color
    {
        private Rgba64 data;

        public Color(Rgba64 pixel)
        {
            this.data = pixel;
        }

        public Color(Rgba32 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        public Color(Vector4 vector)
        {
            this.data = default;
            this.data.FromVector4(vector);
        }

        public static Color FromRgba(byte r, byte g, byte b, byte a) => new Color(new Rgba32(r, g, b, a));

        public TPixel ToPixel<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = default;
            pixel.FromRgba64(this.data);
            return pixel;
        }
    }
}