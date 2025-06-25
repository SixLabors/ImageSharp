// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Converts YCbCr data to rgb data.
/// </summary>
internal class YCbCrConverter
{
    private readonly CodingRangeExpander yExpander;
    private readonly CodingRangeExpander cbExpander;
    private readonly CodingRangeExpander crExpander;
    private readonly YCbCrToRgbConverter converter;

    private static readonly Rational[] DefaultLuma =
    [
        new(299, 1000),
        new(587, 1000),
        new(114, 1000)
    ];

    private static readonly Rational[] DefaultReferenceBlackWhite =
    [
        new(0, 1), new(255, 1),
        new(128, 1), new(255, 1),
        new(128, 1), new(255, 1)
    ];

    public YCbCrConverter(Rational[] referenceBlackAndWhite, Rational[] coefficients)
    {
        referenceBlackAndWhite ??= DefaultReferenceBlackWhite;
        coefficients ??= DefaultLuma;

        if (referenceBlackAndWhite.Length != 6)
        {
            TiffThrowHelper.ThrowImageFormatException("reference black and white array should have 6 entry's");
        }

        if (coefficients.Length != 3)
        {
            TiffThrowHelper.ThrowImageFormatException("luma coefficients array should have 6 entry's");
        }

        this.yExpander = new CodingRangeExpander(referenceBlackAndWhite[0], referenceBlackAndWhite[1], 255);
        this.cbExpander = new CodingRangeExpander(referenceBlackAndWhite[2], referenceBlackAndWhite[3], 127);
        this.crExpander = new CodingRangeExpander(referenceBlackAndWhite[4], referenceBlackAndWhite[5], 127);
        this.converter = new YCbCrToRgbConverter(coefficients[0], coefficients[1], coefficients[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba32 ConvertToRgba32(byte y, byte cb, byte cr)
    {
        float yExpanded = this.yExpander.Expand(y);
        float cbExpanded = this.cbExpander.Expand(cb);
        float crExpanded = this.crExpander.Expand(cr);

        Rgba32 rgba = this.converter.Convert(yExpanded, cbExpanded, crExpanded);

        return rgba;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte RoundAndClampTo8Bit(float value)
    {
        int input = (int)MathF.Round(value);
        return (byte)Numerics.Clamp(input, 0, 255);
    }

    private readonly struct CodingRangeExpander
    {
        private readonly float f1;
        private readonly float f2;

        public CodingRangeExpander(Rational referenceBlack, Rational referenceWhite, int codingRange)
        {
            float black = referenceBlack.ToSingle();
            float white = referenceWhite.ToSingle();
            this.f1 = codingRange / (white - black);
            this.f2 = this.f1 * black;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Expand(float code) => (code * this.f1) - this.f2;
    }

    private readonly struct YCbCrToRgbConverter
    {
        private readonly float cr2R;
        private readonly float cb2B;
        private readonly float y2G;
        private readonly float cr2G;
        private readonly float cb2G;

        public YCbCrToRgbConverter(Rational lumaRed, Rational lumaGreen, Rational lumaBlue)
        {
            this.cr2R = 2 - (2 * lumaRed.ToSingle());
            this.cb2B = 2 - (2 * lumaBlue.ToSingle());
            this.y2G = (1 - lumaBlue.ToSingle() - lumaRed.ToSingle()) / lumaGreen.ToSingle();
            this.cr2G = 2 * lumaRed.ToSingle() * (lumaRed.ToSingle() - 1) / lumaGreen.ToSingle();
            this.cb2G = 2 * lumaBlue.ToSingle() * (lumaBlue.ToSingle() - 1) / lumaGreen.ToSingle();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba32 Convert(float y, float cb, float cr)
        {
            Rgba32 pixel = default(Rgba32);
            pixel.R = RoundAndClampTo8Bit((cr * this.cr2R) + y);
            pixel.G = RoundAndClampTo8Bit((this.y2G * y) + (this.cr2G * cr) + (this.cb2G * cb));
            pixel.B = RoundAndClampTo8Bit((cb * this.cb2B) + y);
            pixel.A = byte.MaxValue;

            return pixel;
        }
    }
}
