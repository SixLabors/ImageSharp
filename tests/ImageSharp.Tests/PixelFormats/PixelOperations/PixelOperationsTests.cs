// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Common;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations;

[Trait("Category", "PixelFormats")]
public partial class PixelOperationsTests
{
    [Theory]
    [WithBlankImages(1, 1, PixelTypes.All)]
    public void GetGlobalInstance<T>(TestImageProvider<T> _)
        where T : unmanaged, IPixel<T> => Assert.NotNull(PixelOperations<T>.Instance);
}

public abstract class PixelOperationsTests<TPixel> : MeasureFixture
    where TPixel : unmanaged, IPixel<TPixel>
{
    public const string SkipProfilingBenchmarks =
#if true
        "Profiling benchmark - enable manually!";
#else
            null;
#endif

    protected PixelOperationsTests(ITestOutputHelper output)
        : base(output)
    {
    }

    public static TheoryData<int> ArraySizesData =>
        new()
        {
            0,
            1,
            2,
            7,
            16,
            512,
            513,
            514,
            515,
            516,
            517,
            518,
            519,
            520,
            521,
            522,
            523,
            524,
            525,
            526,
            527,
            528,
            1111
        };

    protected Configuration Configuration => Configuration.Default;

    protected virtual PixelOperations<TPixel> Operations { get; } = PixelOperations<TPixel>.Instance;

    protected bool HasUnassociatedAlpha => TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Unassociated;

    internal static TPixel[] CreateExpectedPixelData(Vector4[] source, RefAction<Vector4> vectorModifier = null)
    {
        TPixel[] expected = new TPixel[source.Length];

        for (int i = 0; i < expected.Length; i++)
        {
            Vector4 v = source[i];
            vectorModifier?.Invoke(ref v);

            expected[i] = TPixel.FromVector4(v);
        }

        return expected;
    }

    internal static TPixel[] CreateScaledExpectedPixelData(Vector4[] source, RefAction<Vector4> vectorModifier = null)
    {
        TPixel[] expected = new TPixel[source.Length];

        for (int i = 0; i < expected.Length; i++)
        {
            Vector4 v = source[i];
            vectorModifier?.Invoke(ref v);

            expected[i] = TPixel.FromScaledVector4(v);
        }

        return expected;
    }

    [Fact]
    public void PixelTypeInfoHasCorrectBitsPerPixel()
    {
        int bits = TPixel.GetPixelTypeInfo().BitsPerPixel;
        Assert.Equal(Unsafe.SizeOf<TPixel>() * 8, bits);
    }

    [Fact]
    public void PixelAlphaRepresentation_DefinesPresenceOfAlphaChannel()
    {
        // We use 0 - 255 as we have pixel formats that store
        // the alpha component in less than 8 bits.
        const byte alpha = byte.MinValue;
        const byte noAlpha = byte.MaxValue;

        TPixel pixel = TPixel.FromRgba32(new(0, 0, 0, alpha));

        Rgba32 dest = pixel.ToRgba32();

        bool hasAlpha = TPixel.GetPixelTypeInfo().AlphaRepresentation != PixelAlphaRepresentation.None;

        byte expectedAlpha = hasAlpha ? alpha : noAlpha;
        Assert.Equal(expectedAlpha, dest.A);
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromVector4(int count)
    {
        Vector4[] source = CreateVector4TestData(count);
        TPixel[] expected = CreateExpectedPixelData(source);

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromVector4Destructive(this.Configuration, s, d.GetSpan()));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromScaledVector4(int count)
    {
        Vector4[] source = CreateVector4TestData(count);
        TPixel[] expected = CreateScaledExpectedPixelData(source);

        TestOperation(
            source,
            expected,
            (s, d) =>
                {
                    Span<TPixel> destPixels = d.GetSpan();
                    this.Operations.FromVector4Destructive(this.Configuration, s, destPixels, PixelConversionModifiers.Scale);
                });
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromCompandedScaledVector4(int count)
    {
        void SourceAction(ref Vector4 v) => v = SRgbCompanding.Expand(v);

        void ExpectedAction(ref Vector4 v) => v = SRgbCompanding.Compress(v);

        Vector4[] source = CreateVector4TestData(count, SourceAction);
        TPixel[] expected = CreateScaledExpectedPixelData(source, ExpectedAction);

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromVector4Destructive(
                this.Configuration,
                s,
                d.GetSpan(),
                PixelConversionModifiers.SRgbCompand | PixelConversionModifiers.Scale),
            false);
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromPremultipliedVector4(int count)
    {
        void SourceAction(ref Vector4 v)
        {
            if (this.HasUnassociatedAlpha)
            {
                Numerics.Premultiply(ref v);
            }
        }

        void ExpectedAction(ref Vector4 v)
        {
            if (this.HasUnassociatedAlpha)
            {
                Numerics.UnPremultiply(ref v);
            }
        }

        Vector4[] source = CreateVector4TestData(count, (ref Vector4 v) => SourceAction(ref v));
        TPixel[] expected = CreateExpectedPixelData(source, (ref Vector4 v) => ExpectedAction(ref v));

        TestOperation(
            source,
            expected,
            (s, d) =>
            {
                PixelConversionModifiers modifiers = this.HasUnassociatedAlpha
                    ? PixelConversionModifiers.Premultiply
                    : PixelConversionModifiers.None;

                this.Operations.FromVector4Destructive(this.Configuration, s, d.GetSpan(), modifiers);
            });
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromPremultipliedScaledVector4(int count)
    {
        void SourceAction(ref Vector4 v)
        {
            if (this.HasUnassociatedAlpha)
            {
                Numerics.Premultiply(ref v);
            }
        }

        void ExpectedAction(ref Vector4 v)
        {
            if (this.HasUnassociatedAlpha)
            {
                Numerics.UnPremultiply(ref v);
            }
        }

        Vector4[] source = CreateVector4TestData(count, (ref Vector4 v) => SourceAction(ref v));
        TPixel[] expected = CreateScaledExpectedPixelData(source, (ref Vector4 v) => ExpectedAction(ref v));

        TestOperation(
            source,
            expected,
            (s, d) =>
            {
                PixelConversionModifiers modifiers = this.HasUnassociatedAlpha
                    ? PixelConversionModifiers.Premultiply
                    : PixelConversionModifiers.None;

                this.Operations.FromVector4Destructive(
                                    this.Configuration,
                                    s,
                                    d.GetSpan(),
                                    modifiers | PixelConversionModifiers.Scale);
            });
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromCompandedPremultipliedScaledVector4(int count)
    {
        void SourceAction(ref Vector4 v)
        {
            v = SRgbCompanding.Expand(v);

            if (this.HasUnassociatedAlpha)
            {
                Numerics.Premultiply(ref v);
            }
        }

        void ExpectedAction(ref Vector4 v)
        {
            if (this.HasUnassociatedAlpha)
            {
                Numerics.UnPremultiply(ref v);
            }

            v = SRgbCompanding.Compress(v);
        }

        Vector4[] source = CreateVector4TestData(count, SourceAction);
        TPixel[] expected = CreateScaledExpectedPixelData(source, ExpectedAction);

        TestOperation(
            source,
            expected,
            (s, d) =>
            {
                PixelConversionModifiers modifiers = this.HasUnassociatedAlpha
                    ? PixelConversionModifiers.Premultiply
                    : PixelConversionModifiers.None;

                this.Operations.FromVector4Destructive(
                                    this.Configuration,
                                    s,
                                    d.GetSpan(),
                                    modifiers | PixelConversionModifiers.SRgbCompand | PixelConversionModifiers.Scale);
            },
            false);
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToVector4(int count)
    {
        TPixel[] source = CreatePixelTestData(count);
        Vector4[] expected = CreateExpectedVector4Data(source);

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToVector4(this.Configuration, s, d.GetSpan()));
    }

    public static readonly TheoryData<object> Generic_To_Data = new()
    {
        new TestPixel<A8>(),
        new TestPixel<Abgr32>(),
        new TestPixel<Argb32>(),
        new TestPixel<Bgr24>(),
        new TestPixel<Bgr565>(),
        new TestPixel<Bgra32>(),
        new TestPixel<Bgra4444>(),
        new TestPixel<Bgra5551>(),
        new TestPixel<Byte4>(),
        new TestPixel<HalfSingle>(),
        new TestPixel<HalfVector2>(),
        new TestPixel<HalfVector4>(),
        new TestPixel<L16>(),
        new TestPixel<L8>(),
        new TestPixel<La16>(),
        new TestPixel<La32>(),
        new TestPixel<NormalizedByte2>(),
        new TestPixel<NormalizedByte4>(),
        new TestPixel<NormalizedShort2>(),
        new TestPixel<NormalizedShort4>(),
        new TestPixel<Rg32>(),
        new TestPixel<Rgb24>(),
        new TestPixel<Rgb48>(),
        new TestPixel<Rgba1010102>(),
        new TestPixel<Rgba32>(),
        new TestPixel<Rgba64>(),
        new TestPixel<RgbaVector>(),
        new TestPixel<Short2>(),
        new TestPixel<Short4>(),
    };

    [Theory]
    [MemberData(nameof(Generic_To_Data))]
    public void Generic_To<TDestPixel>(TestPixel<TDestPixel> _)
        where TDestPixel : unmanaged, IPixel<TDestPixel>
    {
        const int count = 2134;
        TPixel[] source = CreatePixelTestData(count);
        TDestPixel[] expected = new TDestPixel[count];

        PixelConverterTests.ReferenceImplementations.To<TPixel, TDestPixel>(this.Configuration, source, expected);

        TestOperation(source, expected, (s, d) => this.Operations.To(this.Configuration, s, d.GetSpan()), false);
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToScaledVector4(int count)
    {
        TPixel[] source = CreateScaledPixelTestData(count);
        Vector4[] expected = CreateExpectedScaledVector4Data(source);

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToVector4(
                this.Configuration,
                s,
                d.GetSpan(),
                PixelConversionModifiers.Scale));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToCompandedScaledVector4(int count)
    {
        void SourceAction(ref Vector4 v)
        {
        }

        void ExpectedAction(ref Vector4 v) => v = SRgbCompanding.Expand(v);

        TPixel[] source = CreateScaledPixelTestData(count, SourceAction);
        Vector4[] expected = CreateExpectedScaledVector4Data(source, ExpectedAction);

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToVector4(
                this.Configuration,
                s,
                d.GetSpan(),
                PixelConversionModifiers.SRgbCompand | PixelConversionModifiers.Scale));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToPremultipliedVector4(int count)
    {
        void SourceAction(ref Vector4 v)
        {
        }

        void ExpectedAction(ref Vector4 v) => Numerics.Premultiply(ref v);

        TPixel[] source = CreatePixelTestData(count, SourceAction);
        Vector4[] expected = CreateExpectedVector4Data(source, ExpectedAction);

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToVector4(this.Configuration, s, d.GetSpan(), PixelConversionModifiers.Premultiply));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToPremultipliedScaledVector4(int count)
    {
        void SourceAction(ref Vector4 v)
        {
        }

        void ExpectedAction(ref Vector4 v) => Numerics.Premultiply(ref v);

        TPixel[] source = CreateScaledPixelTestData(count, SourceAction);
        Vector4[] expected = CreateExpectedScaledVector4Data(source, (ref Vector4 v) => ExpectedAction(ref v));

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToVector4(
                this.Configuration,
                s,
                d.GetSpan(),
                PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToCompandedPremultipliedScaledVector4(int count)
    {
        void SourceAction(ref Vector4 v)
        {
        }

        void ExpectedAction(ref Vector4 v)
        {
            v = SRgbCompanding.Expand(v);
            Numerics.Premultiply(ref v);
        }

        TPixel[] source = CreateScaledPixelTestData(count, SourceAction);
        Vector4[] expected = CreateExpectedScaledVector4Data(source, ExpectedAction);

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToVector4(
                this.Configuration,
                s,
                d.GetSpan(),
                PixelConversionModifiers.SRgbCompand | PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromArgb32Bytes(int count)
    {
        byte[] source = CreateByteTestData(count * 4);
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            int i4 = i * 4;

            expected[i] = TPixel.FromArgb32(new(source[i4 + 1], source[i4 + 2], source[i4 + 3], source[i4 + 0]));
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromArgb32Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToArgb32Bytes(int count)
    {
        TPixel[] source = CreatePixelTestData(count);
        byte[] expected = new byte[count * 4];

        for (int i = 0; i < count; i++)
        {
            int i4 = i * 4;
            Argb32 argb = Argb32.FromScaledVector4(source[i].ToScaledVector4());

            expected[i4] = argb.A;
            expected[i4 + 1] = argb.R;
            expected[i4 + 2] = argb.G;
            expected[i4 + 3] = argb.B;
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToArgb32Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromBgr24Bytes(int count)
    {
        byte[] source = CreateByteTestData(count * 3);
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            int i3 = i * 3;

            expected[i] = TPixel.FromBgr24(new(source[i3 + 2], source[i3 + 1], source[i3]));
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromBgr24Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToBgr24Bytes(int count)
    {
        TPixel[] source = CreatePixelTestData(count);
        byte[] expected = new byte[count * 3];

        for (int i = 0; i < count; i++)
        {
            int i3 = i * 3;
            Bgr24 bgr = Bgr24.FromScaledVector4(source[i].ToScaledVector4());
            expected[i3] = bgr.B;
            expected[i3 + 1] = bgr.G;
            expected[i3 + 2] = bgr.R;
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToBgr24Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromBgra32Bytes(int count)
    {
        byte[] source = CreateByteTestData(count * 4);
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            int i4 = i * 4;

            expected[i] = TPixel.FromBgra32(new(source[i4 + 2], source[i4 + 1], source[i4 + 0], source[i4 + 3]));
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromBgra32Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToBgra32Bytes(int count)
    {
        TPixel[] source = CreatePixelTestData(count);
        byte[] expected = new byte[count * 4];

        for (int i = 0; i < count; i++)
        {
            int i4 = i * 4;
            Bgra32 bgra = Bgra32.FromScaledVector4(source[i].ToScaledVector4());
            expected[i4] = bgra.B;
            expected[i4 + 1] = bgra.G;
            expected[i4 + 2] = bgra.R;
            expected[i4 + 3] = bgra.A;
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToBgra32Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromAbgr32Bytes(int count)
    {
        byte[] source = CreateByteTestData(count * 4);
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            int i4 = i * 4;

            expected[i] = TPixel.FromAbgr32(new(source[i4 + 3], source[i4 + 2], source[i4 + 1], source[i4 + 0]));
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromAbgr32Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToAbgr32Bytes(int count)
    {
        TPixel[] source = CreatePixelTestData(count);
        byte[] expected = new byte[count * 4];

        for (int i = 0; i < count; i++)
        {
            int i4 = i * 4;
            Abgr32 abgr = Abgr32.FromScaledVector4(source[i].ToScaledVector4());
            expected[i4] = abgr.A;
            expected[i4 + 1] = abgr.B;
            expected[i4 + 2] = abgr.G;
            expected[i4 + 3] = abgr.R;
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToAbgr32Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromBgra5551Bytes(int count)
    {
        int size = Unsafe.SizeOf<Bgra5551>();
        byte[] source = CreateByteTestData(count * size);
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            int offset = i * size;

            Bgra5551 bgra = MemoryMarshal.Cast<byte, Bgra5551>(source.AsSpan().Slice(offset, size))[0];
            expected[i] = TPixel.FromBgra5551(bgra);
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromBgra5551Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToBgra5551Bytes(int count)
    {
        int size = Unsafe.SizeOf<Bgra5551>();
        TPixel[] source = CreatePixelTestData(count);
        byte[] expected = new byte[count * size];

        for (int i = 0; i < count; i++)
        {
            int offset = i * size;
            Bgra5551 bgra = Bgra5551.FromScaledVector4(source[i].ToScaledVector4());
            OctetBytes bytes = Unsafe.As<Bgra5551, OctetBytes>(ref bgra);
            expected[offset] = bytes[0];
            expected[offset + 1] = bytes[1];
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToBgra5551Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromL8(int count)
    {
        byte[] sourceBytes = CreateByteTestData(count);
        L8[] source = sourceBytes.Select(b => new L8(b)).ToArray();
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            expected[i] = TPixel.FromL8(source[i]);
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromL8(this.Configuration, s, d.GetSpan()));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToL8(int count)
    {
        TPixel[] source = CreatePixelTestData(count);
        L8[] expected = new L8[count];

        for (int i = 0; i < count; i++)
        {
            expected[i] = L8.FromScaledVector4(source[i].ToScaledVector4());
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToL8(this.Configuration, s, d.GetSpan()));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromL16(int count)
    {
        L16[] source = CreateVector4TestData(count).Select(L16.FromVector4).ToArray();

        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            expected[i] = TPixel.FromL16(source[i]);
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromL16(this.Configuration, s, d.GetSpan()));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToL16(int count)
    {
        TPixel[] source = CreatePixelTestData(count);
        L16[] expected = new L16[count];

        for (int i = 0; i < count; i++)
        {
            expected[i] = L16.FromScaledVector4(source[i].ToScaledVector4());
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToL16(this.Configuration, s, d.GetSpan()));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromLa16Bytes(int count)
    {
        int size = Unsafe.SizeOf<La16>();
        byte[] source = CreateByteTestData(count * size);
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            int offset = i * size;

            La16 la = MemoryMarshal.Cast<byte, La16>(source.AsSpan().Slice(offset, size))[0];
            expected[i] = TPixel.FromLa16(la);
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromLa16Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToLa16Bytes(int count)
    {
        int size = Unsafe.SizeOf<La16>();
        TPixel[] source = CreatePixelTestData(count);
        byte[] expected = new byte[count * size];

        for (int i = 0; i < count; i++)
        {
            int offset = i * size;
            La16 la = La16.FromScaledVector4(source[i].ToScaledVector4());
            OctetBytes bytes = Unsafe.As<La16, OctetBytes>(ref la);
            expected[offset] = bytes[0];
            expected[offset + 1] = bytes[1];
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToLa16Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromLa32Bytes(int count)
    {
        int size = Unsafe.SizeOf<La32>();
        byte[] source = CreateByteTestData(count * size);
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            int offset = i * size;

            La32 la = MemoryMarshal.Cast<byte, La32>(source.AsSpan().Slice(offset, size))[0];
            expected[i] = TPixel.FromLa32(la);
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromLa32Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToLa32Bytes(int count)
    {
        int size = Unsafe.SizeOf<La32>();
        TPixel[] source = CreatePixelTestData(count);
        byte[] expected = new byte[count * size];

        for (int i = 0; i < count; i++)
        {
            int offset = i * size;
            La32 la = La32.FromScaledVector4(source[i].ToScaledVector4());
            OctetBytes bytes = Unsafe.As<La32, OctetBytes>(ref la);
            expected[offset] = bytes[0];
            expected[offset + 1] = bytes[1];
            expected[offset + 2] = bytes[2];
            expected[offset + 3] = bytes[3];
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToLa32Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromRgb24Bytes(int count)
    {
        byte[] source = CreateByteTestData(count * 3);
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            int i3 = i * 3;

            expected[i] = TPixel.FromRgb24(new(source[i3 + 0], source[i3 + 1], source[i3 + 2]));
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromRgb24Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToRgb24Bytes(int count)
    {
        TPixel[] source = CreatePixelTestData(count);
        byte[] expected = new byte[count * 3];

        for (int i = 0; i < count; i++)
        {
            int i3 = i * 3;
            Rgb24 rgb = Rgb24.FromScaledVector4(source[i].ToScaledVector4());
            expected[i3] = rgb.R;
            expected[i3 + 1] = rgb.G;
            expected[i3 + 2] = rgb.B;
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToRgb24Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromRgba32Bytes(int count)
    {
        byte[] source = CreateByteTestData(count * 4);
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            int i4 = i * 4;

            expected[i] = TPixel.FromRgba32(new(source[i4 + 0], source[i4 + 1], source[i4 + 2], source[i4 + 3]));
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromRgba32Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToRgba32Bytes(int count)
    {
        TPixel[] source = CreatePixelTestData(count);
        byte[] expected = new byte[count * 4];

        for (int i = 0; i < count; i++)
        {
            int i4 = i * 4;
            Rgba32 rgba = Rgba32.FromScaledVector4(source[i].ToScaledVector4());
            expected[i4] = rgba.R;
            expected[i4 + 1] = rgba.G;
            expected[i4 + 2] = rgba.B;
            expected[i4 + 3] = rgba.A;
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToRgba32Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromRgb48Bytes(int count)
    {
        byte[] source = CreateByteTestData(count * 6);
        Span<byte> sourceSpan = source.AsSpan();
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            int i6 = i * 6;
            expected[i] = TPixel.FromRgb48(MemoryMarshal.Cast<byte, Rgb48>(sourceSpan.Slice(i6, 6))[0]);
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromRgb48Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToRgb48Bytes(int count)
    {
        TPixel[] source = CreatePixelTestData(count);
        byte[] expected = new byte[count * 6];

        for (int i = 0; i < count; i++)
        {
            int i6 = i * 6;
            Rgb48 rgb = Rgb48.FromScaledVector4(source[i].ToScaledVector4());
            OctetBytes rgb48Bytes = Unsafe.As<Rgb48, OctetBytes>(ref rgb);
            expected[i6] = rgb48Bytes[0];
            expected[i6 + 1] = rgb48Bytes[1];
            expected[i6 + 2] = rgb48Bytes[2];
            expected[i6 + 3] = rgb48Bytes[3];
            expected[i6 + 4] = rgb48Bytes[4];
            expected[i6 + 5] = rgb48Bytes[5];
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToRgb48Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void FromRgba64Bytes(int count)
    {
        byte[] source = CreateByteTestData(count * 8);
        Span<byte> sourceSpan = source.AsSpan();
        TPixel[] expected = new TPixel[count];

        for (int i = 0; i < count; i++)
        {
            int i8 = i * 8;
            expected[i] = TPixel.FromRgba64(MemoryMarshal.Cast<byte, Rgba64>(sourceSpan.Slice(i8, 8))[0]);
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.FromRgba64Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void ToRgba64Bytes(int count)
    {
        TPixel[] source = CreatePixelTestData(count);
        byte[] expected = new byte[count * 8];

        for (int i = 0; i < count; i++)
        {
            int i8 = i * 8;
            Rgba64 rgba = Rgba64.FromScaledVector4(source[i].ToScaledVector4());
            OctetBytes rgba64Bytes = Unsafe.As<Rgba64, OctetBytes>(ref rgba);
            expected[i8] = rgba64Bytes[0];
            expected[i8 + 1] = rgba64Bytes[1];
            expected[i8 + 2] = rgba64Bytes[2];
            expected[i8 + 3] = rgba64Bytes[3];
            expected[i8 + 4] = rgba64Bytes[4];
            expected[i8 + 5] = rgba64Bytes[5];
            expected[i8 + 6] = rgba64Bytes[6];
            expected[i8 + 7] = rgba64Bytes[7];
        }

        TestOperation(
            source,
            expected,
            (s, d) => this.Operations.ToRgba64Bytes(this.Configuration, s, d.GetSpan(), count));
    }

    [Theory]
    [MemberData(nameof(ArraySizesData))]
    public void PackFromRgbPlanes(int count)
        => SimdUtilsTests.TestPackFromRgbPlanes<TPixel>(
            count,
            (r, g, b, actual) => PixelOperations<TPixel>.Instance.PackFromRgbPlanes(r, g, b, actual));

    public delegate void RefAction<T1>(ref T1 arg1);

    internal static Vector4[] CreateExpectedVector4Data(TPixel[] source, RefAction<Vector4> vectorModifier = null)
    {
        Vector4[] expected = new Vector4[source.Length];

        for (int i = 0; i < expected.Length; i++)
        {
            Vector4 v = source[i].ToVector4();

            vectorModifier?.Invoke(ref v);

            expected[i] = v;
        }

        return expected;
    }

    internal static Vector4[] CreateExpectedScaledVector4Data(TPixel[] source, RefAction<Vector4> vectorModifier = null)
    {
        Vector4[] expected = new Vector4[source.Length];

        for (int i = 0; i < expected.Length; i++)
        {
            Vector4 v = source[i].ToScaledVector4();

            vectorModifier?.Invoke(ref v);

            expected[i] = v;
        }

        return expected;
    }

    internal static void TestOperation<TSource, TDest>(
        TSource[] source,
        TDest[] expected,
        Action<TSource[], IMemoryOwner<TDest>> action,
        bool preferExactComparison = true)
        where TSource : struct
        where TDest : struct
    {
        using (TestBuffers<TSource, TDest> buffers = new(source, expected, preferExactComparison))
        {
            action(buffers.SourceBuffer, buffers.ActualDestBuffer);
            buffers.Verify();
        }
    }

    internal static Vector4[] CreateVector4TestData(int length, RefAction<Vector4> vectorModifier = null)
    {
        Vector4[] result = new Vector4[length];
        Random rnd = new(42); // Deterministic random values

        for (int i = 0; i < result.Length; i++)
        {
            Vector4 v = GetScaledVector(rnd);
            vectorModifier?.Invoke(ref v);

            result[i] = v;
        }

        return result;
    }

    internal static TPixel[] CreatePixelTestData(int length, RefAction<Vector4> vectorModifier = null)
    {
        TPixel[] result = new TPixel[length];

        Random rnd = new(42); // Deterministic random values

        for (int i = 0; i < result.Length; i++)
        {
            Vector4 v = GetScaledVector(rnd);

            vectorModifier?.Invoke(ref v);

            result[i] = TPixel.FromVector4(v);
        }

        return result;
    }

    internal static TPixel[] CreateScaledPixelTestData(int length, RefAction<Vector4> vectorModifier = null)
    {
        TPixel[] result = new TPixel[length];

        Random rnd = new(42); // Deterministic random values

        for (int i = 0; i < result.Length; i++)
        {
            Vector4 v = GetScaledVector(rnd);

            vectorModifier?.Invoke(ref v);

            result[i] = TPixel.FromScaledVector4(v);
        }

        return result;
    }

    internal static byte[] CreateByteTestData(int length, int seed = 42)
    {
        byte[] result = new byte[length];
        Random rnd = new(seed); // Deterministic random values

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (byte)rnd.Next(255);
        }

        return result;
    }

    internal static Vector4 GetScaledVector(Random rnd)
        => new((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble());

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct OctetBytes
    {
        public fixed byte Data[8];

        public byte this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ref byte self = ref Unsafe.As<OctetBytes, byte>(ref this);
                return Unsafe.Add(ref self, idx);
            }
        }
    }

    private class TestBuffers<TSource, TDest> : IDisposable
        where TSource : struct
        where TDest : struct
    {
        public TSource[] SourceBuffer { get; }

        public IMemoryOwner<TDest> ActualDestBuffer { get; }

        public TDest[] ExpectedDestBuffer { get; }

        public bool PreferExactComparison { get; }

        public TestBuffers(TSource[] source, TDest[] expectedDest, bool preferExactComparison = true)
        {
            this.SourceBuffer = source;
            this.ExpectedDestBuffer = expectedDest;
            this.ActualDestBuffer = Configuration.Default.MemoryAllocator.Allocate<TDest>(expectedDest.Length);
            this.PreferExactComparison = preferExactComparison;
        }

        public void Dispose() => this.ActualDestBuffer.Dispose();

        public void Verify()
        {
            int count = this.ExpectedDestBuffer.Length;

            if (typeof(TDest) == typeof(Vector4))
            {
                Span<Vector4> expected = MemoryMarshal.Cast<TDest, Vector4>(this.ExpectedDestBuffer.AsSpan());
                Span<Vector4> actual = MemoryMarshal.Cast<TDest, Vector4>(this.ActualDestBuffer.GetSpan());
                ApproximateFloatComparer comparer = new(TestEnvironment.Is64BitProcess ? 0.0001F : 0.001F);

                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(expected[i], actual[i], comparer);
                }
            }
            else if (!this.PreferExactComparison && typeof(IPixel).IsAssignableFrom(typeof(TDest)) && IsComplexPixel())
            {
                Span<TDest> expected = this.ExpectedDestBuffer.AsSpan();
                Span<TDest> actual = this.ActualDestBuffer.GetSpan();
                ApproximateFloatComparer comparer = new(TestEnvironment.Is64BitProcess ? 0.0001F : 0.001F);

                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(((IPixel)expected[i]).ToScaledVector4(), ((IPixel)actual[i]).ToScaledVector4(), comparer);
                }
            }
            else
            {
                Span<TDest> expected = this.ExpectedDestBuffer.AsSpan();
                Span<TDest> actual = this.ActualDestBuffer.GetSpan();

                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(expected[i], actual[i]);
                }
            }
        }

        // TODO: Figure out a means to use PixelTypeInfo here.
        private static bool IsComplexPixel() => default(TDest) switch
        {
            HalfSingle or HalfVector2 or L16 or La32 or NormalizedShort2 or Rg32 or Short2 => true,
            _ => Unsafe.SizeOf<TDest>() > sizeof(int),
        };
    }
}
