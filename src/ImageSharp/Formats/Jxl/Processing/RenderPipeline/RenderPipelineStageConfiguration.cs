// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal readonly record struct RenderPipelineStageConfiguration(int BorderX, int BorderY, int ShiftX, int ShiftY)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderPipelineStageConfiguration CreateShiftX(int shift, int border) => new(border, 0, shift, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderPipelineStageConfiguration CreateShiftY(int shift, int border) => new(0, border, 0, shift);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderPipelineStageConfiguration CreateSymmetric(int shift, int border) => new(border, border, shift, shift);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RenderPipelineStageConfiguration CreateSymmetricBorderOnly(int border) => CreateSymmetric(shift: 0, border);
}
