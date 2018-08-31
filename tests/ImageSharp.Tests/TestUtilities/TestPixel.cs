// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;

using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.TestUtilities
{
    public class TestPixel<TPixel> : IXunitSerializable
        where TPixel : struct, IPixel<TPixel>
    {
        public TestPixel()
        {
        }

        public TestPixel(float red, float green, float blue, float alpha)
        {
            this.Red = red;
            this.Green = green;
            this.Blue = blue;
            this.Alpha = alpha;
        }

        public float Red { get; set; }
        public float Green { get; set; }
        public float Blue { get; set; }
        public float Alpha { get; set; }

        public static implicit operator TPixel(TestPixel<TPixel> d)
        {
            return d?.AsPixel() ?? default(TPixel);
        }

        public TPixel AsPixel()
        {
            TPixel pix = default(TPixel);
            pix.PackFromVector4(new System.Numerics.Vector4(this.Red, this.Green, this.Blue, this.Alpha));
            return pix;
        }

        internal Span<TPixel> AsSpan()
        {
            return new Span<TPixel>(new[] { AsPixel() });
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            this.Red = info.GetValue<float>("red");
            this.Green = info.GetValue<float>("green");
            this.Blue = info.GetValue<float>("blue");
            this.Alpha = info.GetValue<float>("alpha");
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("red", this.Red);
            info.AddValue("green", this.Green);
            info.AddValue("blue", this.Blue);
            info.AddValue("alpha", this.Alpha);
        }

        public override string ToString()
        {
            return $"{typeof(TPixel).Name}{this.AsPixel().ToString()}";
        }
    }
}
