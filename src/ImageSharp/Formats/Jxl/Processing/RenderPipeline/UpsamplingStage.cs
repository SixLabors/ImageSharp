// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal class UpsamplingStage : RenderPipelineStageBase
{
    private const int ChunkSize = 1024;

    private readonly float[] kernel = new float[64 * 25]; // 1600 floats, 6400 bytes (6.25 KB)
    private readonly int c;
    private readonly InlineArray3<List<Memory<byte>>> temp = default;

    private void PreprocessRowImpl(Buffer2D<Memory<float>> inputRows, int x0, int length, int threadId)
    {
        Span<float> colMin = MemoryMarshal.Cast<byte, float>(this.temp[0][threadId].Span);
        Span<float> colMax = MemoryMarshal.Cast<byte, float>(this.temp[1][threadId].Span);

        InlineArray5<Memory<float>> rows = default;
        rows[0] = 
    }
}
