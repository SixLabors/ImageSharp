// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;

internal sealed class ButteraugliBlurTemp : IDisposable
{
    public JxlPlane<float> TransposedTemp { get; set; } = new();

    public JxlPlane<float> GetTransposed(Configuration configuration, JxlPlane<float> input)
    {
        if (this.TransposedTemp.XSize == 0)
        {
            // Yes, YSize and XSize are swapped
            this.TransposedTemp = JxlPlane<float>.Create(configuration, input.YSize, input.XSize);
        }

        return this.TransposedTemp;
    }

    public void Dispose() => this.TransposedTemp.Dispose();
}
