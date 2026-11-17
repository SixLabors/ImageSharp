// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats;

public class EncodingUtilitiesTests
{
    /// <summary>
    /// Checks the packed index layout of BMP, PNG and TIFF rows on each vector width and on the scalar path.
    /// </summary>
    /// <param name="bitsPerIndex">The bit count of one index.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void PackIndices_PacksFirstIndexInHighBits(int bitsPerIndex)
    {
        static void RunTest(string serialized)
        {
            int bits = FeatureTestRunner.Deserialize<int>(serialized);
            int indicesPerByte = 8 / bits;
            Random random = new(42);

            // These lengths end before, at and after each vector step: 16, 32 and 64 packed bytes. They also end inside
            // a byte at each depth.
            int[] lengths = [0, 1, 2, 3, 5, 7, 8, 9, 15, 16, 17, 31, 32, 33, 63, 64, 65, 127, 128, 129, 255, 256, 257, 511, 512, 513, 1023, 1024, 1031];
            foreach (int length in lengths)
            {
                byte[] indices = new byte[length];
                for (int i = 0; i < indices.Length; i++)
                {
                    indices[i] = (byte)random.Next(1 << bits);
                }

                // The formats store the first index of a byte in its most significant bits. Each later index is the
                // next bits below. A row that ends inside a byte leaves the low bits of that byte zero.
                int packedLength = ((length * bits) + 7) / 8;
                byte[] expected = new byte[packedLength + 3];
                for (int i = 0; i < indices.Length; i++)
                {
                    expected[i / indicesPerByte] |= (byte)(indices[i] << (8 - bits - ((i % indicesPerByte) * bits)));
                }

                // The bytes after the packed row must not change. The BMP encoder keeps its row padding there.
                expected.AsSpan(packedLength).Fill(0xA5);
                byte[] actual = new byte[expected.Length];
                actual.AsSpan().Fill(0xA5);

                EncodingUtilities.PackIndices(indices, actual, bits);

                Assert.Equal(expected, actual);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            bitsPerIndex,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }
}
