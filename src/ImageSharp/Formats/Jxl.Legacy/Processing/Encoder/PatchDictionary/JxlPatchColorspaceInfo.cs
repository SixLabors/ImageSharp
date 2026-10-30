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
            ((ReadOnlySpan<float>)[0.01615f, 0.08875f, 0.1922f]).CopyTo(this.ChannelDequant);
            ((ReadOnlySpan<float>)[30.0f, 3.0f, 1.0f]).CopyTo(this.ChannelWeights);
        }
        else
        {
            ((ReadOnlySpan<float>)[20.0f / 255, 22.0f / 255, 20.0f / 255]).CopyTo(this.ChannelDequant);
            ((ReadOnlySpan<float>)[0.017f * 255, 0.02f * 255, 0.017f * 255]).CopyTo(this.ChannelWeights);
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

        distance += MathF.Abs(v1[2] - v2[2]) * this.ChannelWeights[2];
        distance += MathF.Abs(v1[1] - v2[1]) * this.ChannelWeights[1];
        distance += MathF.Abs(v1[0] - v2[0]) * this.ChannelWeights[0];

        return distance <= threshold;
    }
}
