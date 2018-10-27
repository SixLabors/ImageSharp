// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations
{
    public partial class PixelOperationsTests
    {
        public class Gray8OperationsTests : PixelOperationsTests<Gray8>
        {
            public Gray8OperationsTests(ITestOutputHelper output)
                : base(output)
            {
            }

            [Fact]
            public void IsSpecialImplementation() => Assert.IsType<Gray8.PixelOperations>(PixelOperations<Gray8>.Instance);

            [Theory]
            [MemberData(nameof(ArraySizesData))]
            public void FromGray8Bytes(int count)
            {
                byte[] source = CreateByteTestData(count);
                var expected = new Gray8[count];

                for (int i = 0; i < count; i++)
                {
                    expected[i].FromGray8(new Gray8(source[i]));
                }

                TestOperation(
                    source,
                    expected,
                    (s, d) => Operations.FromGray8Bytes(this.Configuration, s, d.GetSpan(), count)
                );
            }

            [Theory]
            [MemberData(nameof(ArraySizesData))]
            public void ToGray8Bytes(int count)
            {
                Gray8[] source = CreatePixelTestData(count);
                byte[] expected = new byte[count];
                var gray = default(Gray8);

                for (int i = 0; i < count; i++)
                {
                    gray.FromScaledVector4(source[i].ToScaledVector4());
                    expected[i] = gray.PackedValue;
                }

                TestOperation(
                    source,
                    expected,
                    (s, d) => Operations.ToGray8Bytes(this.Configuration, s, d.GetSpan(), count)
                );
            }

            [Theory]
            [MemberData(nameof(ArraySizesData))]
            public void FromGray16Bytes(int count)
            {
                byte[] source = CreateByteTestData(count * 2);
                Span<byte> sourceSpan = source.AsSpan();
                var expected = new Gray8[count];

                for (int i = 0; i < count; i++)
                {
                    int i2 = i * 2;
                    expected[i].FromGray16(MemoryMarshal.Cast<byte, Gray16>(sourceSpan.Slice(i2, 2))[0]);
                }

                TestOperation(
                    source,
                    expected,
                    (s, d) => Operations.FromGray16Bytes(this.Configuration, s, d.GetSpan(), count)
                );
            }

            [Theory]
            [MemberData(nameof(ArraySizesData))]
            public void ToGray16Bytes(int count)
            {
                Gray8[] source = CreatePixelTestData(count);
                byte[] expected = new byte[count * 2];
                Gray16 gray = default;

                for (int i = 0; i < count; i++)
                {
                    int i2 = i * 2;
                    gray.FromScaledVector4(source[i].ToScaledVector4());
                    OctetBytes bytes = Unsafe.As<Gray16, OctetBytes>(ref gray);
                    expected[i2] = bytes[0];
                    expected[i2 + 1] = bytes[1];
                }

                TestOperation(
                    source,
                    expected,
                    (s, d) => Operations.ToGray16Bytes(this.Configuration, s, d.GetSpan(), count)
                );
            }
        }
    }
}