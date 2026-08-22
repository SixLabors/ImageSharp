// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

[StructLayout(LayoutKind.Sequential)]
internal struct JxlNoiseLevel(float noiseLevel, float intensity)
{
    public float NoiseLevel = noiseLevel;

    public float Intensity = intensity;
}
