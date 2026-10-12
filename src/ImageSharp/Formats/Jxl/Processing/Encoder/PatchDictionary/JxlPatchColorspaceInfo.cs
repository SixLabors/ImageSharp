// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.PatchDictionary;

internal struct JxlPatchColorspaceInfo
{
    public InlineArray3<float> ChannelDequant;
    public InlineArray3<float> ChannelWeights;

    public JxlPatchColorspaceInfo(bool isXyb)
    {
        if (isXyb)
        {
            this.ChannelDequant[0] = 0.01615f;
            this.ChannelDequant[1] = 0.08875f;
            this.ChannelDequant[2] = 0.1922f;
            this.ChannelWeights[0] = 30.0f;
            this.ChannelWeights[1] = 3.0f;
            this.ChannelWeights[2] = 1.0f;
        }
        else
        {
            this.ChannelDequant[0] = 20.0f / 255;
            this.ChannelDequant[1] = 22.0f / 255;
            this.ChannelDequant[2] = 20.0f / 255;
            this.ChannelWeights[0] = 0.017f * 255;
            this.ChannelWeights[1] = 0.02f * 255;
            this.ChannelWeights[2] = 0.017f * 255;
        }
    }

    public static explicit operator JxlPatchColorspaceInfo(bool isXyb) => new(isXyb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float ScaleForQuantization(float value, int c) => value / this.ChannelDequant[c];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int Quantize(float value, int c)
    {
        float scaled = this.ScaleForQuantization(value, c);
        float clamped = Math.Clamp(scaled, -32768.0f, 32767.0f);
        return (int)MathF.Truncate(clamped);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsSimilar(Span<int> v1, Span<int> v2, float threshold)
    {
        float distance = 0;

        for (int c = 0; c < 3; c++)
        {
            distance += MathF.Abs(v1[c] - v2[c]) * this.ChannelWeights[c];
        }

        return distance <= threshold;
    }
}
