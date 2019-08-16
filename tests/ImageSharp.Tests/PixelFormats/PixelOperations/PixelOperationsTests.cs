// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorSpaces.Companding;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations
{
    public partial class PixelOperationsTests
    {
        [Theory]
        [WithBlankImages(1, 1, PixelTypes.All)]
        public void GetGlobalInstance<T>(TestImageProvider<T> _)
            where T : struct, IPixel<T> => Assert.NotNull(PixelOperations<T>.Instance);
    }

    public abstract class PixelOperationsTests<TPixel> : MeasureFixture
        where TPixel : struct, IPixel<TPixel>
    {
        public const string SkipProfilingBenchmarks =
#if true
            "Profiling benchmark - enable manually!";
#else
                null;
#endif

        protected bool HasAlpha { get; set; } = true;

        protected PixelOperationsTests(ITestOutputHelper output)
            : base(output)
        {
        }

        public static TheoryData<int> ArraySizesData =>
            new TheoryData<int>
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

        internal static PixelOperations<TPixel> Operations => PixelOperations<TPixel>.Instance;

        internal static TPixel[] CreateExpectedPixelData(Vector4[] source, RefAction<Vector4> vectorModifier = null)
        {
            var expected = new TPixel[source.Length];

            for (int i = 0; i < expected.Length; i++)
            {
                Vector4 v = source[i];
                vectorModifier?.Invoke(ref v);

                expected[i].FromVector4(v);
            }

            return expected;
        }

        internal static TPixel[] CreateScaledExpectedPixelData(Vector4[] source, RefAction<Vector4> vectorModifier = null)
        {
            var expected = new TPixel[source.Length];

            for (int i = 0; i < expected.Length; i++)
            {
                Vector4 v = source[i];
                vectorModifier?.Invoke(ref v);

                expected[i].FromScaledVector4(v);
            }

            return expected;
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
                (s, d) => Operations.FromVector4Destructive(this.Configuration, s, d.GetSpan())
            );
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
                        Operations.FromVector4Destructive(this.Configuration, (Span<Vector4>)s, destPixels, PixelConversionModifiers.Scale);
                    });
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void FromCompandedScaledVector4(int count)
        {
            void sourceAction(ref Vector4 v)
            {
                SRgbCompanding.Expand(ref v);
            }

            void expectedAction(ref Vector4 v)
            {
                SRgbCompanding.Compress(ref v);
            }

            Vector4[] source = CreateVector4TestData(count, (ref Vector4 v) => sourceAction(ref v));
            TPixel[] expected = CreateScaledExpectedPixelData(source, (ref Vector4 v) => expectedAction(ref v));

            TestOperation(
                source,
                expected,
                (s, d) => Operations.FromVector4Destructive(
                    this.Configuration,
                    s,
                    d.GetSpan(),
                    PixelConversionModifiers.SRgbCompand | PixelConversionModifiers.Scale)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void FromPremultipliedVector4(int count)
        {
            void sourceAction(ref Vector4 v)
            {
                if (this.HasAlpha)
                {
                    Vector4Utils.Premultiply(ref v);
                }
            }

            void expectedAction(ref Vector4 v)
            {
                if (this.HasAlpha)
                {
                    Vector4Utils.UnPremultiply(ref v);
                }
            }

            Vector4[] source = CreateVector4TestData(count, (ref Vector4 v) => sourceAction(ref v));
            TPixel[] expected = CreateExpectedPixelData(source, (ref Vector4 v) => expectedAction(ref v));

            TestOperation(
                source,
                expected,
                (s, d) => Operations.FromVector4Destructive(this.Configuration, s, d.GetSpan(), PixelConversionModifiers.Premultiply)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void FromPremultipliedScaledVector4(int count)
        {
            void sourceAction(ref Vector4 v)
            {
                if (this.HasAlpha)
                {
                    Vector4Utils.Premultiply(ref v);
                }
            }

            void expectedAction(ref Vector4 v)
            {
                if (this.HasAlpha)
                {
                    Vector4Utils.UnPremultiply(ref v);
                }
            }

            Vector4[] source = CreateVector4TestData(count, (ref Vector4 v) => sourceAction(ref v));
            TPixel[] expected = CreateScaledExpectedPixelData(source, (ref Vector4 v) => expectedAction(ref v));

            TestOperation(
                source,
                expected,
                (s, d) => Operations.FromVector4Destructive(
                    this.Configuration,
                    s,
                    d.GetSpan(),
                    PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void FromCompandedPremultipliedScaledVector4(int count)
        {
            void sourceAction(ref Vector4 v)
            {
                SRgbCompanding.Expand(ref v);

                if (this.HasAlpha)
                {
                    Vector4Utils.Premultiply(ref v);
                }
            }

            void expectedAction(ref Vector4 v)
            {
                if (this.HasAlpha)
                {
                    Vector4Utils.UnPremultiply(ref v);
                }

                SRgbCompanding.Compress(ref v);
            }

            Vector4[] source = CreateVector4TestData(count, (ref Vector4 v) => sourceAction(ref v));
            TPixel[] expected = CreateScaledExpectedPixelData(source, (ref Vector4 v) => expectedAction(ref v));

            TestOperation(
                source,
                expected,
                (s, d) => Operations.FromVector4Destructive(
                    this.Configuration,
                    s,
                    d.GetSpan(),
                    PixelConversionModifiers.SRgbCompand | PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale)
            );
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
                (s, d) => Operations.ToVector4(this.Configuration, s, d.GetSpan())
            );
        }


        public static readonly TheoryData<IPixel> Generic_To_Data = new TheoryData<IPixel>
                                                    {
                                                        default(Rgba32),
                                                        default(Bgra32),
                                                        default(Rgb24),
                                                        default(Gray8),
                                                        default(Gray16),
                                                        default(Rgb48),
                                                        default(Rgba64)
                                                    };

        [Theory]
        [MemberData(nameof(Generic_To_Data))]
        public void Generic_To<TDestPixel>(TDestPixel dummy)
            where TDestPixel : struct, IPixel<TDestPixel>
        {
            const int Count = 2134;
            TPixel[] source = CreatePixelTestData(Count);
            var expected = new TDestPixel[Count];

            PixelConverterTests.ReferenceImplementations.To<TPixel, TDestPixel>(this.Configuration, source, expected);

            TestOperation(source, expected, (s, d) => Operations.To(this.Configuration, (ReadOnlySpan<TPixel>)s, d.GetSpan()));
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
                (s, d) =>
                    {
                        Span<Vector4> destVectors = d.GetSpan();
                        Operations.ToVector4(this.Configuration, (ReadOnlySpan<TPixel>)s, destVectors, PixelConversionModifiers.Scale);
                    });
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToCompandedScaledVector4(int count)
        {
            void sourceAction(ref Vector4 v)
            {
                SRgbCompanding.Compress(ref v);
            }

            void expectedAction(ref Vector4 v)
            {
                SRgbCompanding.Expand(ref v);
            }

            TPixel[] source = CreateScaledPixelTestData(count, (ref Vector4 v) => sourceAction(ref v));
            Vector4[] expected = CreateExpectedScaledVector4Data(source, (ref Vector4 v) => expectedAction(ref v));

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToVector4(
                    this.Configuration,
                    s,
                    d.GetSpan(),
                    PixelConversionModifiers.SRgbCompand | PixelConversionModifiers.Scale)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToPremultipliedVector4(int count)
        {
            void sourceAction(ref Vector4 v)
            {
                Vector4Utils.UnPremultiply(ref v);
            }

            void expectedAction(ref Vector4 v)
            {
                Vector4Utils.Premultiply(ref v);
            }

            TPixel[] source = CreatePixelTestData(count, (ref Vector4 v) => sourceAction(ref v));
            Vector4[] expected = CreateExpectedVector4Data(source, (ref Vector4 v) => expectedAction(ref v));

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToVector4(this.Configuration, s, d.GetSpan(), PixelConversionModifiers.Premultiply)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToPremultipliedScaledVector4(int count)
        {
            void sourceAction(ref Vector4 v)
            {
                Vector4Utils.UnPremultiply(ref v);
            }

            void expectedAction(ref Vector4 v)
            {
                Vector4Utils.Premultiply(ref v);
            }

            TPixel[] source = CreateScaledPixelTestData(count, (ref Vector4 v) => sourceAction(ref v));
            Vector4[] expected = CreateExpectedScaledVector4Data(source, (ref Vector4 v) => expectedAction(ref v));

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToVector4(
                    this.Configuration,
                    s,
                    d.GetSpan(),
                    PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale));
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToCompandedPremultipliedScaledVector4(int count)
        {
            void sourceAction(ref Vector4 v)
            {
                Vector4Utils.UnPremultiply(ref v);
                SRgbCompanding.Compress(ref v);
            }

            void expectedAction(ref Vector4 v)
            {
                SRgbCompanding.Expand(ref v);
                Vector4Utils.Premultiply(ref v);
            }

            TPixel[] source = CreateScaledPixelTestData(count, (ref Vector4 v) => sourceAction(ref v));
            Vector4[] expected = CreateExpectedScaledVector4Data(source, (ref Vector4 v) => expectedAction(ref v));

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToVector4(
                    this.Configuration,
                    s,
                    d.GetSpan(),
                    PixelConversionModifiers.SRgbCompand | PixelConversionModifiers.Premultiply | PixelConversionModifiers.Scale)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void FromArgb32Bytes(int count)
        {
            byte[] source = CreateByteTestData(count * 4);
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;

                expected[i].FromArgb32(new Argb32(source[i4 + 1], source[i4 + 2], source[i4 + 3], source[i4 + 0]));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.FromArgb32Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToArgb32Bytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            var expected = new byte[count * 4];
            var argb = default(Argb32);

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                argb.FromScaledVector4(source[i].ToScaledVector4());

                expected[i4] = argb.A;
                expected[i4 + 1] = argb.R;
                expected[i4 + 2] = argb.G;
                expected[i4 + 3] = argb.B;
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToArgb32Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void FromBgr24Bytes(int count)
        {
            byte[] source = CreateByteTestData(count * 3);
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;

                expected[i].FromBgr24(new Bgr24(source[i3 + 2], source[i3 + 1], source[i3]));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.FromBgr24Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToBgr24Bytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            var expected = new byte[count * 3];
            var bgr = default(Bgr24);

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;
                bgr.FromScaledVector4(source[i].ToScaledVector4());
                expected[i3] = bgr.B;
                expected[i3 + 1] = bgr.G;
                expected[i3 + 2] = bgr.R;
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToBgr24Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void FromBgra32Bytes(int count)
        {
            byte[] source = CreateByteTestData(count * 4);
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;

                expected[i].FromBgra32(new Bgra32(source[i4 + 2], source[i4 + 1], source[i4 + 0], source[i4 + 3]));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.FromBgra32Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToBgra32Bytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            var expected = new byte[count * 4];
            var bgra = default(Bgra32);

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                bgra.FromScaledVector4(source[i].ToScaledVector4());
                expected[i4] = bgra.B;
                expected[i4 + 1] = bgra.G;
                expected[i4 + 2] = bgra.R;
                expected[i4 + 3] = bgra.A;
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToBgra32Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void FromRgb24Bytes(int count)
        {
            byte[] source = CreateByteTestData(count * 3);
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;

                expected[i].FromRgb24(new Rgb24(source[i3 + 0], source[i3 + 1], source[i3 + 2]));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.FromRgb24Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToRgb24Bytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            var expected = new byte[count * 3];
            var rgb = default(Rgb24);

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;
                rgb.FromScaledVector4(source[i].ToScaledVector4());
                expected[i3] = rgb.R;
                expected[i3 + 1] = rgb.G;
                expected[i3 + 2] = rgb.B;
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToRgb24Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void FromRgba32Bytes(int count)
        {
            byte[] source = CreateByteTestData(count * 4);
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;

                expected[i].FromRgba32(new Rgba32(source[i4 + 0], source[i4 + 1], source[i4 + 2], source[i4 + 3]));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.FromRgba32Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToRgba32Bytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            var expected = new byte[count * 4];
            var rgba = default(Rgba32);

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                rgba.FromScaledVector4(source[i].ToScaledVector4());
                expected[i4] = rgba.R;
                expected[i4 + 1] = rgba.G;
                expected[i4 + 2] = rgba.B;
                expected[i4 + 3] = rgba.A;
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToRgba32Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void FromRgb48Bytes(int count)
        {
            byte[] source = CreateByteTestData(count * 6);
            Span<byte> sourceSpan = source.AsSpan();
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i6 = i * 6;
                expected[i].FromRgb48(MemoryMarshal.Cast<byte, Rgb48>(sourceSpan.Slice(i6, 6))[0]);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.FromRgb48Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToRgb48Bytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            var expected = new byte[count * 6];
            Rgb48 rgb = default;

            for (int i = 0; i < count; i++)
            {
                int i6 = i * 6;
                rgb.FromScaledVector4(source[i].ToScaledVector4());
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
                (s, d) => Operations.ToRgb48Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void FromRgba64Bytes(int count)
        {
            byte[] source = CreateByteTestData(count * 8);
            Span<byte> sourceSpan = source.AsSpan();
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i8 = i * 8;
                expected[i].FromRgba64(MemoryMarshal.Cast<byte, Rgba64>(sourceSpan.Slice(i8, 8))[0]);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.FromRgba64Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToRgba64Bytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            var expected = new byte[count * 8];
            Rgba64 rgba = default;

            for (int i = 0; i < count; i++)
            {
                int i8 = i * 8;
                rgba.FromScaledVector4(source[i].ToScaledVector4());
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
                (s, d) => Operations.ToRgba64Bytes(this.Configuration, s, d.GetSpan(), count)
            );
        }


            [Theory]
            [MemberData(nameof(ArraySizesData))]
            public void FromGray8(int count)
            {
                byte[] sourceBytes = CreateByteTestData(count);
                Gray8[] source = sourceBytes.Select(b => new Gray8(b)).ToArray();
                var expected = new TPixel[count];


                for (int i = 0; i < count; i++)
                {
                    expected[i].FromGray8(source[i]);
                }

                TestOperation(
                    source,
                    expected,
                    (s, d) => Operations.FromGray8(this.Configuration, s, d.GetSpan())
                );
            }

            [Theory]
            [MemberData(nameof(ArraySizesData))]
            public void ToGray8(int count)
            {
                TPixel[] source = CreatePixelTestData(count);
                var expected = new Gray8[count];

                for (int i = 0; i < count; i++)
                {
                    expected[i].FromScaledVector4(source[i].ToScaledVector4());
                }

                TestOperation(
                    source,
                    expected,
                    (s, d) => Operations.ToGray8(this.Configuration, s, d.GetSpan())
                );
            }

            [Theory]
            [MemberData(nameof(ArraySizesData))]
            public void FromGray16(int count)
            {
                Gray16[] source = CreateVector4TestData(count).Select(v =>
                {
                    Gray16 g = default;
                    g.FromVector4(v);
                    return g;
                }).ToArray();

                var expected = new TPixel[count];

                for (int i = 0; i < count; i++)
                {
                    expected[i].FromGray16(source[i]);
                }

                TestOperation(
                    source,
                    expected,
                    (s, d) => Operations.FromGray16(this.Configuration, s, d.GetSpan())
                );
            }

            [Theory]
            [MemberData(nameof(ArraySizesData))]
            public void ToGray16(int count)
            {
                TPixel[] source = CreatePixelTestData(count);
                var expected = new Gray16[count];

                for (int i = 0; i < count; i++)
                {
                    expected[i].FromScaledVector4(source[i].ToScaledVector4());
                }

                TestOperation(
                    source,
                    expected,
                    (s, d) => Operations.ToGray16(this.Configuration, s, d.GetSpan())
                );
            }

        public delegate void RefAction<T1>(ref T1 arg1);

        internal static Vector4[] CreateExpectedVector4Data(TPixel[] source, RefAction<Vector4> vectorModifier = null)
        {
            var expected = new Vector4[source.Length];

            for (int i = 0; i < expected.Length; i++)
            {
                var v = source[i].ToVector4();

                vectorModifier?.Invoke(ref v);

                expected[i] = v;
            }

            return expected;
        }

        internal static Vector4[] CreateExpectedScaledVector4Data(TPixel[] source, RefAction<Vector4> vectorModifier = null)
        {
            var expected = new Vector4[source.Length];

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
            Action<TSource[], IMemoryOwner<TDest>> action)
            where TSource : struct
            where TDest : struct
        {
            using (var buffers = new TestBuffers<TSource, TDest>(source, expected))
            {
                action(buffers.SourceBuffer, buffers.ActualDestBuffer);
                buffers.Verify();
            }
        }

        internal static Vector4[] CreateVector4TestData(int length, RefAction<Vector4> vectorModifier = null)
        {
            var result = new Vector4[length];
            var rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                Vector4 v = GetVector(rnd);
                vectorModifier?.Invoke(ref v);

                result[i] = v;
            }
            return result;
        }

        internal static TPixel[] CreatePixelTestData(int length, RefAction<Vector4> vectorModifier = null)
        {
            var result = new TPixel[length];

            var rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                Vector4 v = GetVector(rnd);

                vectorModifier?.Invoke(ref v);

                result[i].FromVector4(v);
            }

            return result;
        }

        internal static TPixel[] CreateScaledPixelTestData(int length, RefAction<Vector4> vectorModifier = null)
        {
            var result = new TPixel[length];

            var rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                Vector4 v = GetVector(rnd);

                vectorModifier?.Invoke(ref v);

                result[i].FromScaledVector4(v);
            }

            return result;
        }

        internal static byte[] CreateByteTestData(int length)
        {
            var result = new byte[length];
            var rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (byte)rnd.Next(255);
            }
            return result;
        }

        internal static Vector4 GetVector(Random rnd)
        {
            return new Vector4(
                (float)rnd.NextDouble(),
                (float)rnd.NextDouble(),
                (float)rnd.NextDouble(),
                (float)rnd.NextDouble()
            );
        }

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

            public TestBuffers(TSource[] source, TDest[] expectedDest)
            {
                this.SourceBuffer = source;
                this.ExpectedDestBuffer = expectedDest;
                this.ActualDestBuffer = Configuration.Default.MemoryAllocator.Allocate<TDest>(expectedDest.Length);
            }

            public void Dispose() => this.ActualDestBuffer.Dispose();

            public void Verify()
            {
                int count = this.ExpectedDestBuffer.Length;

                if (typeof(TDest) == typeof(Vector4))
                {
                    Span<Vector4> expected = MemoryMarshal.Cast<TDest, Vector4>(this.ExpectedDestBuffer.AsSpan());
                    Span<Vector4> actual = MemoryMarshal.Cast<TDest, Vector4>(this.ActualDestBuffer.GetSpan());

                    var comparer = new ApproximateFloatComparer(0.001f);
                    for (int i = 0; i < count; i++)
                    {
                        // ReSharper disable PossibleNullReferenceException
                        Assert.Equal(expected[i], actual[i], comparer);
                        // ReSharper restore PossibleNullReferenceException
                    }
                }
                else if (typeof(TDest) == typeof(Gray16))
                {
                    // Minor difference is tolerated for 16 bit pixel values
                    Span<Gray16> expected = MemoryMarshal.Cast<TDest, Gray16>(this.ExpectedDestBuffer.AsSpan());
                    Span<Gray16> actual = MemoryMarshal.Cast<TDest, Gray16>(this.ActualDestBuffer.GetSpan());

                    for (int i = 0; i < count; i++)
                    {
                        int difference = expected[i].PackedValue - actual[i].PackedValue;
                        Assert.True(Math.Abs(difference) < 2);
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
        }
    }
}
