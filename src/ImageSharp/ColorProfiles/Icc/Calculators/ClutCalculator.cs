// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

/// <summary>
/// Implements interpolation methods for color profile lookup tables.
/// </summary>
internal class ClutCalculator : IVector4Calculator
{
    private readonly bool useTrilinearInterpolation;
    private readonly int inputCount;
    private readonly int outputCount;
    private readonly float[] lut;
    private readonly byte[] gridPointCount;
    private readonly byte[] maxGridPoint;
    private readonly int[] dimSize;
    private const int LowerCorner = 0;
    private readonly int n001;
    private readonly int n010;
    private readonly int n011;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClutCalculator"/> class.
    /// </summary>
    /// <param name="clut">The table to evaluate.</param>
    /// <param name="useTrilinearInterpolation">Whether tables use multilinear interpolation.</param>
    public ClutCalculator(IccClut clut, bool useTrilinearInterpolation)
    {
        Guard.NotNull(clut, nameof(clut));

        // This calculator consumes and produces Vector4 values. A table may describe
        // more channels, but it cannot be evaluated through this four-channel contract.
        Guard.MustBeBetweenOrEqualTo(clut.InputChannelCount, 1, 4, nameof(clut.InputChannelCount));
        Guard.MustBeBetweenOrEqualTo(clut.OutputChannelCount, 1, 4, nameof(clut.OutputChannelCount));

        this.useTrilinearInterpolation = useTrilinearInterpolation;
        this.inputCount = clut.InputChannelCount;
        this.outputCount = clut.OutputChannelCount;
        this.lut = clut.Values;
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

        this.n001 = this.dimSize[0];
        if (this.inputCount == 2)
        {
            this.n010 = this.dimSize[1];
            this.n011 = this.n001 + this.n010;
        }
    }

    /// <inheritdoc/>
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
                if (this.useTrilinearInterpolation)
                {
                    this.Interpolate3d((float*)&value, (float*)&result);
                }
                else
                {
                    this.InterpolateTetrahedral((float*)&value, (float*)&result);
                }

                break;
            case 4:
                if (this.useTrilinearInterpolation)
                {
                    this.Interpolate4d((float*)&value, (float*)&result);
                }
                else
                {
                    this.InterpolateTetrahedral((float*)&value, (float*)&result);
                }

                break;
        }

        return result;
    }

    /// <summary>
    /// One dimensional interpolation function.
    /// </summary>
    /// <param name="srcPixel">The input pixel values, which will be interpolated.</param>
    /// <param name="destPixel">The interpolated output pixels.</param>
    private unsafe void Interpolate1d(float* srcPixel, float* destPixel)
    {
        byte mx = this.maxGridPoint[0];

        float x = Numerics.Clamp(srcPixel[0], 0F, 1F) * mx;

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
            destPixel[i] = (float)((p[offset + LowerCorner] * dF0) + (p[offset + this.n001] * dF1));
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

        float x = Numerics.Clamp(srcPixel[0], 0F, 1F) * mx;
        float y = Numerics.Clamp(srcPixel[1], 0F, 1F) * my;

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
            destPixel[i] = (float)((p[offset + LowerCorner] * dF0) + (p[offset + this.n001] * dF1) + (p[offset + this.n010] * dF2) + (p[offset + this.n011] * dF3));
            offset++;
        }
    }

    /// <summary>
    /// Interpolates a three-channel table independently along each axis.
    /// </summary>
    /// <param name="srcPixel">The input pixel values, which will be interpolated.</param>
    /// <param name="destPixel">The interpolated output pixels.</param>
    private unsafe void Interpolate3d(float* srcPixel, float* destPixel)
    {
        int xStride = this.dimSize[0];
        int yStride = this.dimSize[1];
        int zStride = this.dimSize[2];

        byte mx = this.maxGridPoint[0];
        byte my = this.maxGridPoint[1];
        byte mz = this.maxGridPoint[2];

        float x = Numerics.Clamp(srcPixel[0], 0F, 1F) * mx;
        float y = Numerics.Clamp(srcPixel[1], 0F, 1F) * my;
        float z = Numerics.Clamp(srcPixel[2], 0F, 1F) * mz;

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

        Span<float> p = this.lut.AsSpan((int)((ix * xStride) + (iy * yStride) + (iz * zStride)));

        // The eight corner weights are products of the independent axis fractions.
        // This tensor-product blend is used for Lab-indexed output tables.
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
            destPixel[i] = (float)((p[offset + 0] * dF0) +
                                   (p[offset + xStride] * dF1) +
                                   (p[offset + yStride] * dF2) +
                                   (p[offset + (xStride + yStride)] * dF3) +
                                   (p[offset + zStride] * dF4) +
                                   (p[offset + (xStride + zStride)] * dF5) +
                                   (p[offset + (yStride + zStride)] * dF6) +
                                   (p[offset + (xStride + yStride + zStride)] * dF7));
            offset++;
        }
    }

    /// <summary>
    /// Interpolates three-channel tables or blends tetrahedral slices of four-channel tables.
    /// </summary>
    /// <param name="srcPixel">The input pixel values, which will be interpolated.</param>
    /// <param name="destPixel">The interpolated output pixels.</param>
    private unsafe void InterpolateTetrahedral(float* srcPixel, float* destPixel)
    {
        int dimension = this.inputCount - 3;
        int tableOffset = 0;
        int sliceStride = 0;
        float fraction = 0F;
        if (this.inputCount == 4)
        {
            float position = Numerics.Clamp(srcPixel[0], 0F, 1F) * this.maxGridPoint[0];
            int lowerSlice = (int)position;
            fraction = position - lowerSlice;
            tableOffset = lowerSlice * this.dimSize[0];
            sliceStride = lowerSlice == this.maxGridPoint[0] ? 0 : this.dimSize[0];
            srcPixel++;
        }

        // Adjacent slices have the same grid and input coordinates. Compute their cell
        // and tetrahedron once; only the first-axis offset differs between the slices.
        int xStride = this.dimSize[dimension];
        int yStride = this.dimSize[dimension + 1];
        int zStride = this.dimSize[dimension + 2];

        byte mx = this.maxGridPoint[dimension];
        byte my = this.maxGridPoint[dimension + 1];
        byte mz = this.maxGridPoint[dimension + 2];

        float x = Numerics.Clamp(srcPixel[0], 0F, 1F) * mx;
        float y = Numerics.Clamp(srcPixel[1], 0F, 1F) * my;
        float z = Numerics.Clamp(srcPixel[2], 0F, 1F) * mz;

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

        // The fractional coordinates select one of six tetrahedra sharing the cell's
        // lower and upper corners. Walking the axes from largest fraction to smallest
        // identifies the two intermediate vertices. Choose once for all output channels.
        int firstVertex;
        int secondVertex;
        float firstWeight;
        float secondWeight;
        float thirdWeight;

        if (u >= t)
        {
            if (t >= s)
            {
                firstVertex = xStride;
                secondVertex = xStride + yStride;
                firstWeight = u;
                secondWeight = t;
                thirdWeight = s;
            }
            else if (u >= s)
            {
                firstVertex = xStride;
                secondVertex = xStride + zStride;
                firstWeight = u;
                secondWeight = s;
                thirdWeight = t;
            }
            else
            {
                firstVertex = zStride;
                secondVertex = xStride + zStride;
                firstWeight = s;
                secondWeight = u;
                thirdWeight = t;
            }
        }
        else if (u >= s)
        {
            firstVertex = yStride;
            secondVertex = xStride + yStride;
            firstWeight = t;
            secondWeight = u;
            thirdWeight = s;
        }
        else if (t >= s)
        {
            firstVertex = yStride;
            secondVertex = yStride + zStride;
            firstWeight = t;
            secondWeight = s;
            thirdWeight = u;
        }
        else
        {
            firstVertex = zStride;
            secondVertex = yStride + zStride;
            firstWeight = s;
            secondWeight = t;
            thirdWeight = u;
        }

        ReadOnlySpan<float> cell = this.lut.AsSpan(tableOffset + (int)((ix * xStride) + (iy * yStride) + (iz * zStride)));

        // Interpolate along the tetrahedron's three edges. Sorted fractions give vertex
        // weights 1-first, first-second, second-third, and third, which sum to one.
        // An input at the upper boundary uses the preceding cell with fraction one;
        // equal fractions give a shared face or edge the same value from either side.
        int upperVertex = xStride + yStride + zStride;
        if (this.inputCount == 3)
        {
            for (int i = 0; i < this.outputCount; i++)
            {
                float lower = cell[i];
                float first = cell[i + firstVertex];
                float second = cell[i + secondVertex];
                float upper = cell[i + upperVertex];
                destPixel[i] = lower
                    + ((first - lower) * firstWeight)
                    + ((second - first) * secondWeight)
                    + ((upper - second) * thirdWeight);
            }
        }
        else
        {
            // Evaluate corresponding vertices in both slices and immediately blend the
            // channel results. At the upper boundary both slices address the same cell.
            ReadOnlySpan<float> upperCell = cell[sliceStride..];
            for (int i = 0; i < this.outputCount; i++)
            {
                float lower = cell[i];
                float first = cell[i + firstVertex];
                float second = cell[i + secondVertex];
                float upper = cell[i + upperVertex];
                float lowerValue = lower
                    + ((first - lower) * firstWeight)
                    + ((second - first) * secondWeight)
                    + ((upper - second) * thirdWeight);

                lower = upperCell[i];
                first = upperCell[i + firstVertex];
                second = upperCell[i + secondVertex];
                upper = upperCell[i + upperVertex];
                float upperValue = lower
                    + ((first - lower) * firstWeight)
                    + ((second - first) * secondWeight)
                    + ((upper - second) * thirdWeight);

                destPixel[i] = lowerValue + ((upperValue - lowerValue) * fraction);
            }
        }
    }

    /// <summary>
    /// Interpolates the sixteen corners surrounding a four-channel input.
    /// </summary>
    /// <param name="srcPixel">The normalized input channels.</param>
    /// <param name="destPixel">The interpolated output channels, initially zero.</param>
    private unsafe void Interpolate4d(float* srcPixel, float* destPixel)
    {
        // Each lane holds one input axis. At the upper boundary, the lower and upper
        // corner share an index, so a zero stride keeps every lookup inside the table.
        Vector4 position = Numerics.Clamp(new Vector4(srcPixel[0], srcPixel[1], srcPixel[2], srcPixel[3]), Vector4.Zero, Vector4.One)
            * new Vector4(this.maxGridPoint[0], this.maxGridPoint[1], this.maxGridPoint[2], this.maxGridPoint[3]);

        int w = (int)position.X;
        int x = (int)position.Y;
        int y = (int)position.Z;
        int z = (int)position.W;
        Vector4 fraction = position - new Vector4(w, x, y, z);
        Vector4 inverse = Vector4.One - fraction;
        int offset = (w * this.dimSize[0]) + (x * this.dimSize[1]) + (y * this.dimSize[2]) + (z * this.dimSize[3]);
        int dw = w == this.maxGridPoint[0] ? 0 : this.dimSize[0];
        int dx = x == this.maxGridPoint[1] ? 0 : this.dimSize[1];
        int dy = y == this.maxGridPoint[2] ? 0 : this.dimSize[2];
        int dz = z == this.maxGridPoint[3] ? 0 : this.dimSize[3];

        // The low bit selects the first axis. Multiply weights from the last axis
        // to the first, and reuse each corner's weight across all output channels.
        for (int corner = 0; corner < 16; corner++)
        {
            float weight = ((corner & 8) == 0 ? inverse.W : fraction.W)
                * ((corner & 4) == 0 ? inverse.Z : fraction.Z)
                * ((corner & 2) == 0 ? inverse.Y : fraction.Y)
                * ((corner & 1) == 0 ? inverse.X : fraction.X);

            int index = offset
                + ((corner & 1) == 0 ? 0 : dw)
                + ((corner & 2) == 0 ? 0 : dx)
                + ((corner & 4) == 0 ? 0 : dy)
                + ((corner & 8) == 0 ? 0 : dz);

            for (int channel = 0; channel < this.outputCount; channel++)
            {
                destPixel[channel] += this.lut[index + channel] * weight;
            }
        }
    }
}
