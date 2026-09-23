// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Noise;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal unsafe struct StrengthEvaluationLut
{
    private fixed byte high16Lut[16];
    private fixed byte low16Lut[16];

    public StrengthEvaluationLut(JxlNoiseParameters noise)
    {
        Span<uint> lut = stackalloc uint[JxlNoiseParameters.NoisePoints];

        // We do a direct reinterpret from float to uint
        noise.Lookup.AsSpan().CopyTo(MemoryMarshal.Cast<uint, float>(lut));

        int i2 = 0;     // i * 2
        int i21 = 1;    // (i * 2) + 1
        for (int i = 0; i < JxlNoiseParameters.NoisePoints; i++)
        {
            uint currLut = lut[i];

            this.low16Lut[i2] = (byte)((currLut >> 0) & 0xFF);
            this.low16Lut[i21] = (byte)((currLut >> 8) & 0xFF);
            this.high16Lut[i2] = (byte)((currLut >> 16) & 0xFF);
            this.high16Lut[i21] = (byte)((currLut >> 24) & 0xFF);

            i2 += 2;
            i21 += 2;
        }
    }

    public Vector<float> Compute(Vector<float> vx)
    {
        fixed (byte* pHigh16Lut = this.high16Lut)
        {
            fixed (byte* pLow16Lut = this.low16Lut)
            {
                const int scale = JxlNoiseParameters.NoisePoints - 2;

                Vector<float> scaledVx = Vector.Max(Vector<float>.Zero, vx * Vector.Create((float)scale));
                Vector<float> floorX = Vector.Floor(scaledVx);
                Vector<float> fracX = scaledVx - floorX;

                floorX = Vector.ConditionalSelect(
                    Vector.GreaterThan(scaledVx, Vector.Create((float)scale + 1)),
                    Vector.Create((float)scale),
                    floorX);
                fracX = Vector.ConditionalSelect(
                    Vector.GreaterThan(scaledVx, Vector.Create((float)scale + 1)),
                    Vector<float>.One,
                    fracX);

                Vector<int> floorXInt = Vector.ConvertToInt32Native(floorX);
                Vector<int> floorXIndicesLow = (floorXInt * Vector.Create(0x0202)) + Vector.Create(0x100);
                Vector<int> floorXIndicesHigh = (floorXInt * Vector.Create(0x2020000)) + Vector.Create(0x01000000);

                Vector<byte> low16 = JxlSimdUtils.LoadDuplicate128(Vector128.Load(pLow16Lut));
                Vector<byte> hi16 = JxlSimdUtils.LoadDuplicate128(Vector128.Load(pHigh16Lut));
                Vector<int> lowm = Vector.Create(0xFFFF); // Low mask
                Vector<int> him = Vector.Create(unchecked((int)0xFFFF0000)); // High mask

                Vector<float> low = ((SimdUtils.GatherBytes(low16, floorXIndicesLow) & lowm) | (SimdUtils.GatherBytes(hi16, floorXIndicesHigh) & him)).As<int, float>();

                floorXIndicesLow += Vector.Create(0x202);
                floorXIndicesHigh += Vector.Create(0x20200000);

                Vector<float> high = ((SimdUtils.GatherBytes(low16, floorXIndicesLow) & lowm) | (SimdUtils.GatherBytes(hi16, floorXIndicesHigh) & him)).As<int, float>();

                return ((high - low) * fracX) + low;
            }
        }
    }
}
