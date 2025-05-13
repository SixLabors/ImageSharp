// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

/// <summary>
/// Implements interpolation methods for color profile lookup tables.
/// Adapted from ICC Reference implementation:
/// https://github.com/InternationalColorConsortium/DemoIccMAX/blob/79ecb74135ad47bac7d42692905a079839b7e105/IccProfLib/IccTagLut.cpp
/// </summary>
internal class ClutCalculator : IVector4Calculator
{
    private readonly int inputCount;
    private readonly int outputCount;
    private readonly float[] lut;
    private readonly byte[] gridPointCount;
    private readonly byte[] maxGridPoint;
    private readonly int[] indexFactor;
    private readonly int[] dimSize;
    private readonly int nodeCount;
    private readonly float[][] nodes;
    private readonly float[] g;
    private readonly uint[] ig;
    private readonly float[] s;
    private readonly float[] df;
    private readonly uint[] nPower;
    private int n000;
    private int n001;
    private int n010;
    private int n011;
    private int n100;
    private int n101;
    private int n110;
    private int n111;
    private int n1000;

    public ClutCalculator(IccClut clut)
    {
        Guard.NotNull(clut, nameof(clut));
        Guard.MustBeGreaterThan(clut.InputChannelCount, 0, nameof(clut.InputChannelCount));
        Guard.MustBeGreaterThan(clut.OutputChannelCount, 0, nameof(clut.OutputChannelCount));

        this.inputCount = clut.InputChannelCount;
        this.outputCount = clut.OutputChannelCount;
        this.g = new float[this.inputCount];
        this.ig = new uint[this.inputCount];
        this.s = new float[this.inputCount];
        this.nPower = new uint[16];
        this.lut = clut.Values;
        this.nodeCount = (int)Math.Pow(2, clut.InputChannelCount);
        this.df = new float[this.nodeCount];
        this.nodes = new float[this.nodeCount][];
        this.dimSize = new int[this.inputCount];
        this.gridPointCount = clut.GridPointCount;
        this.maxGridPoint = new byte[this.inputCount];
        for (int i = 0; i < this.inputCount; i++)
        {
            this.maxGridPoint[i] = (byte)(this.gridPointCount[i] - 1);
        }

        this.dimSize[this.inputCount - 1] = this.outputCount;
        for (int i = this.inputCount - 2; i >= 0; i--)
        {
            this.dimSize[i] = this.dimSize[i + 1] * this.gridPointCount[i + 1];
        }

        this.indexFactor = this.CalculateIndexFactor();
    }

    public unsafe Vector4 Calculate(Vector4 value)
    {
        Vector4 result = default;
        switch (this.inputCount)
        {
            case 1:
                this.Interpolate1d((float*)&value, (float*)&result);
                break;
            case 2:
                this.Interpolate2d((float*)&value, (float*)&result);
                break;
            case 3:
                this.Interpolate3d((float*)&value, (float*)&result);
                break;
            case 4:
                this.Interpolate4d((float*)&value, (float*)&result);
                break;
            default:
                this.InterpolateNd((float*)&value, (float*)&result);
                break;
        }

        return result;
    }

    private int[] CalculateIndexFactor()
    {
        int[] factors = new int[16];
        switch (this.inputCount)
        {
            case 1:
                factors[0] = this.n000 = 0;
                factors[1] = this.n001 = this.dimSize[0];
                break;
            case 2:
                factors[0] = this.n000 = 0;
                factors[1] = this.n001 = this.dimSize[0];
                factors[2] = this.n010 = this.dimSize[1];
                factors[3] = this.n011 = this.n001 + this.n010;
                break;
            case 3:
                factors[0] = this.n000 = 0;
                factors[1] = this.n001 = this.dimSize[0];
                factors[2] = this.n010 = this.dimSize[1];
                factors[3] = this.n011 = this.n001 + this.n010;
                factors[4] = this.n100 = this.dimSize[2];
                factors[5] = this.n101 = this.n100 + this.n001;
                factors[6] = this.n110 = this.n100 + this.n010;
                factors[7] = this.n111 = this.n110 + this.n001;
                break;
            case 4:
                factors[0] = 0;
                factors[1] = this.n001 = this.dimSize[0];
                factors[2] = this.n010 = this.dimSize[1];
                factors[3] = factors[2] + factors[1];
                factors[4] = this.n100 = this.dimSize[2];
                factors[5] = factors[4] + factors[1];
                factors[6] = factors[4] + factors[2];
                factors[7] = factors[4] + factors[3];
                factors[8] = this.n1000 = this.dimSize[3];
                factors[9] = factors[8] + factors[1];
                factors[10] = factors[8] + factors[2];
                factors[11] = factors[8] + factors[3];
                factors[12] = factors[8] + factors[4];
                factors[13] = factors[8] + factors[5];
                factors[14] = factors[8] + factors[6];
                factors[15] = factors[8] + factors[7];
                break;
            default:
                // Initialize ND interpolation variables.
                factors[0] = 0;
                int count;
                for (count = 0; count < this.inputCount; count++)
                {
                    this.nPower[count] = (uint)(1 << (this.inputCount - 1 - count));
                }

                uint[] nPower = [0, 1];
                count = 0;
                int nFlag = 1;
                for (uint j = 1; j < this.nodeCount; j++)
                {
                    if (j == nPower[1])
                    {
                        factors[j] = this.dimSize[count];
                        nPower[0] = (uint)(1 << count);
                        count++;
                        nPower[1] = (uint)(1 << count);
                        nFlag = 1;
                    }
                    else
                    {
                        factors[j] = factors[nPower[0]] + factors[nFlag];
                        nFlag++;
                    }
                }

                break;
        }

        return factors;
    }

    /// <summary>
    /// One dimensional interpolation function.
    /// </summary>
    /// <param name="srcPixel">The input pixel values, which will be interpolated.</param>
    /// <param name="destPixel">The interpolated output pixels.</param>
    private unsafe void Interpolate1d(float* srcPixel, float* destPixel)
    {
        byte mx = this.maxGridPoint[0];

        float x = UnitClip(srcPixel[0]) * mx;

        uint ix = (uint)x;

        float u = x - ix;

        if (ix == mx)
        {
            ix--;
            u = 1.0f;
        }

        float nu = (float)(1.0 - u);

        int i;
        Span<float> p = this.lut.AsSpan((int)(ix * this.n001));

        // Normalize grid units.
        float dF0 = nu;
        float dF1 = u;

        int offset = 0;
        for (i = 0; i < this.outputCount; i++)
        {
            destPixel[i] = (float)((p[offset + this.n000] * dF0) + (p[offset + this.n001] * dF1));
            offset++;
        }
    }

    /// <summary>
    /// Two dimensional interpolation function.
    /// </summary>
    /// <param name="srcPixel">The input pixel values, which will be interpolated.</param>
    /// <param name="destPixel">The interpolated output pixels.</param>
    private unsafe void Interpolate2d(float* srcPixel, float* destPixel)
    {
        byte mx = this.maxGridPoint[0];
        byte my = this.maxGridPoint[1];

        float x = UnitClip(srcPixel[0]) * mx;
        float y = UnitClip(srcPixel[1]) * my;

        uint ix = (uint)x;
        uint iy = (uint)y;

        float u = x - ix;
        float t = y - iy;

        if (ix == mx)
        {
            ix--;
            u = 1.0f;
        }

        if (iy == my)
        {
            iy--;
            t = 1.0f;
        }

        float nt = (float)(1.0 - t);
        float nu = (float)(1.0 - u);

        int i;
        Span<float> p = this.lut.AsSpan((int)((ix * this.n001) + (iy * this.n010)));

        // Normalize grid units.
        float dF0 = nt * nu;
        float dF1 = nt * u;
        float dF2 = t * nu;
        float dF3 = t * u;

        int offset = 0;
        for (i = 0; i < this.outputCount; i++)
        {
            destPixel[i] = (float)((p[offset + this.n000] * dF0) + (p[offset + this.n001] * dF1) + (p[offset + this.n010] * dF2) + (p[offset + this.n011] * dF3));
            offset++;
        }
    }

    /// <summary>
    /// Three dimensional interpolation function.
    /// </summary>
    /// <param name="srcPixel">The input pixel values, which will be interpolated.</param>
    /// <param name="destPixel">The interpolated output pixels.</param>
    private unsafe void Interpolate3d(float* srcPixel, float* destPixel)
    {
        byte mx = this.maxGridPoint[0];
        byte my = this.maxGridPoint[1];
        byte mz = this.maxGridPoint[2];

        float x = UnitClip(srcPixel[0]) * mx;
        float y = UnitClip(srcPixel[1]) * my;
        float z = UnitClip(srcPixel[2]) * mz;

        uint ix = (uint)x;
        uint iy = (uint)y;
        uint iz = (uint)z;

        float u = x - ix;
        float t = y - iy;
        float s = z - iz;

        if (ix == mx)
        {
            ix--;
            u = 1.0f;
        }

        if (iy == my)
        {
            iy--;
            t = 1.0f;
        }

        if (iz == mz)
        {
            iz--;
            s = 1.0f;
        }

        float ns = (float)(1.0 - s);
        float nt = (float)(1.0 - t);
        float nu = (float)(1.0 - u);

        Span<float> p = this.lut.AsSpan((int)((ix * this.n001) + (iy * this.n010) + (iz * this.n100)));

        // Normalize grid units
        float dF0 = ns * nt * nu;
        float dF1 = ns * nt * u;
        float dF2 = ns * t * nu;
        float dF3 = ns * t * u;
        float dF4 = s * nt * nu;
        float dF5 = s * nt * u;
        float dF6 = s * t * nu;
        float dF7 = s * t * u;

        int offset = 0;
        for (int i = 0; i < this.outputCount; i++)
        {
            destPixel[i] = (float)((p[offset + this.n000] * dF0) +
                                   (p[offset + this.n001] * dF1) +
                                   (p[offset + this.n010] * dF2) +
                                   (p[offset + this.n011] * dF3) +
                                   (p[offset + this.n100] * dF4) +
                                   (p[offset + this.n101] * dF5) +
                                   (p[offset + this.n110] * dF6) +
                                   (p[offset + this.n111] * dF7));
            offset++;
        }
    }

    /// <summary>
    /// Four dimensional interpolation function.
    /// </summary>
    /// <param name="srcPixel">The input pixel values, which will be interpolated.</param>
    /// <param name="destPixel">The interpolated output pixels.</param>
    private unsafe void Interpolate4d(float* srcPixel, float* destPixel)
    {
        byte mw = this.maxGridPoint[0];
        byte mx = this.maxGridPoint[1];
        byte my = this.maxGridPoint[2];
        byte mz = this.maxGridPoint[3];

        float w = UnitClip(srcPixel[0]) * mw;
        float x = UnitClip(srcPixel[1]) * mx;
        float y = UnitClip(srcPixel[2]) * my;
        float z = UnitClip(srcPixel[3]) * mz;

        uint iw = (uint)w;
        uint ix = (uint)x;
        uint iy = (uint)y;
        uint iz = (uint)z;

        float v = w - iw;
        float u = x - ix;
        float t = y - iy;
        float s = z - iz;

        if (iw == mw)
        {
            iw--;
            v = 1.0f;
        }

        if (ix == mx)
        {
            ix--;
            u = 1.0f;
        }

        if (iy == my)
        {
            iy--;
            t = 1.0f;
        }

        if (iz == mz)
        {
            iz--;
            s = 1.0f;
        }

        float ns = (float)(1.0 - s);
        float nt = (float)(1.0 - t);
        float nu = (float)(1.0 - u);
        float nv = (float)(1.0 - v);

        Span<float> p = this.lut.AsSpan((int)((iw * this.n001) + (ix * this.n010) + (iy * this.n100) + (iz * this.n1000)));

        // Normalize grid units.
        float[] dF =
        [
            ns * nt * nu * nv,
            ns * nt * nu * v,
            ns * nt * u * nv,
            ns * nt * u * v,
            ns * t * nu * nv,
            ns * t * nu * v,
            ns * t * u * nv,
            ns * t * u * v,
            s * nt * nu * nv,
            s * nt * nu * v,
            s * nt * u * nv,
            s * nt * u * v,
            s * t * nu * nv,
            s * t * nu * v,
            s * t * u * nv,
            s * t * u * v,
        ];

        int offset = 0;
        for (int i = 0; i < this.outputCount; i++)
        {
            float pv = 0.0f;
            for (int j = 0; j < 16; j++)
            {
                pv += p[offset + this.indexFactor[j]] * dF[j];
            }

            destPixel[i] = pv;
            offset++;
        }
    }

    /// <summary>
    /// Generic N-dimensional interpolation function.
    /// </summary>
    /// <param name="srcPixel">The input pixel values, which will be interpolated.</param>
    /// <param name="destPixel">The interpolated output pixels.</param>
    private unsafe void InterpolateNd(float* srcPixel, float* destPixel)
    {
        int index = 0;
        for (int i = 0; i < this.inputCount; i++)
        {
            this.g[i] = UnitClip(srcPixel[i]) * this.maxGridPoint[i];
            this.ig[i] = (uint)this.g[i];
            this.s[this.inputCount - 1 - i] = this.g[i] - this.ig[i];
            if (this.ig[i] == this.maxGridPoint[i])
            {
                this.ig[i]--;
                this.s[this.inputCount - 1 - i] = 1.0f;
            }

            index += (int)this.ig[i] * this.dimSize[i];
        }

        Span<float> p = this.lut.AsSpan(index);
        float[] temp = new float[2];
        bool nFlag = false;

        for (int i = 0; i < this.nodeCount; i++)
        {
            this.df[i] = 1.0f;
        }

        for (int i = 0; i < this.inputCount; i++)
        {
            temp[0] = 1.0f - this.s[i];
            temp[1] = this.s[i];
            index = (int)this.nPower[i];
            for (int j = 0; j < this.nodeCount; j++)
            {
                this.df[j] *= temp[nFlag ? 1 : 0];
                if ((j + 1) % index == 0)
                {
                    nFlag = !nFlag;
                }
            }

            nFlag = false;
        }

        int offset = 0;
        for (int i = 0; i < this.outputCount; i++)
        {
            float pv = 0;
            for (int j = 0; j < this.nodeCount; j++)
            {
                pv += p[offset + this.indexFactor[j]] * this.df[j];
            }

            destPixel[i] = pv;
            offset++;
        }
    }

    private static float UnitClip(float v)
    {
        if (v < 0)
        {
            return 0;
        }

        if (v > 1.0)
        {
            return 1.0f;
        }

        return v;
    }
}
