// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    public readonly struct Color
    {
        private readonly Rgba64 data;

        public Color(Rgba64 pixel)
        {
            this.data = pixel;
        }

        public Color(Rgba32 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        public Color(Argb32 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        public Color(Bgra32 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        public Color(Rgb24 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        public Color(Bgr24 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        public Color(Vector4 vector)
        {
            this.data = new Rgba64(vector);
        }

        public static implicit operator Color(Rgba64 source) => new Color(source);

        public static implicit operator Color(Rgba32 source) => new Color(source);

        public static implicit operator Color(Bgra32 source) => new Color(source);

        public static implicit operator Color(Argb32 source) => new Color(source);

        public static implicit operator Color(Rgb24 source) => new Color(source);

        public static implicit operator Color(Bgr24 source) => new Color(source);

        public static implicit operator Rgba64(Color color) => color.data;

        public static implicit operator Rgba32(Color color) => color.data.ToRgba32();

        public static implicit operator Bgra32(Color color) => color.data.ToBgra32();

        public static implicit operator Argb32(Color color) => color.data.ToArgb32();

        public static implicit operator Rgb24(Color color) => color.data.ToRgb24();

        public static implicit operator Bgr24(Color color) => color.data.ToBgr24();

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