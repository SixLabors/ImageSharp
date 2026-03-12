// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.TestUtilities;

public class TestPixel<TPixel> : IXunitSerializable
    where TPixel : unmanaged, IPixel<TPixel>
{
    public TestPixel()
    {
    }

    public TestPixel(float red, float green, float blue, float alpha)
    {
        Guard.MustBeBetweenOrEqualTo(red, 0F, 1F, nameof(red));
        Guard.MustBeBetweenOrEqualTo(green, 0F, 1F, nameof(green));
        Guard.MustBeBetweenOrEqualTo(blue, 0F, 1F, nameof(blue));
        Guard.MustBeBetweenOrEqualTo(alpha, 0F, 1F, nameof(alpha));

        this.Red = red;
        this.Green = green;
        this.Blue = blue;
        this.Alpha = alpha;
    }

    public float Red { get; set; }

    public float Green { get; set; }

    public float Blue { get; set; }

    public float Alpha { get; set; }

    public TPixel AsPixel() => TPixel.FromScaledVector4(new Vector4(this.Red, this.Green, this.Blue, this.Alpha));

    internal Span<TPixel> AsSpan() => new([this.AsPixel()]);

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

    public override string ToString() => $"{typeof(TPixel).Name}{this.AsPixel()}";
}
