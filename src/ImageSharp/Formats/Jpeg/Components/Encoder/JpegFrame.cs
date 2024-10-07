// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;

/// <summary>
/// Represent a single jpeg frame.
/// </summary>
internal sealed class JpegFrame : IDisposable
{
    public JpegFrame(Image image, JpegFrameConfig frameConfig, bool interleaved)
    {
        this.ColorSpace = frameConfig.ColorType;

        this.Interleaved = interleaved;

        this.PixelWidth = image.Width;
        this.PixelHeight = image.Height;

        MemoryAllocator allocator = image.Configuration.MemoryAllocator;

        JpegComponentConfig[] componentConfigs = frameConfig.Components;
        this.Components = new Component[componentConfigs.Length];
        for (int i = 0; i < this.Components.Length; i++)
        {
            JpegComponentConfig componentConfig = componentConfigs[i];
            this.Components[i] = new(allocator, componentConfig.HorizontalSampleFactor, componentConfig.VerticalSampleFactor, componentConfig.QuantizatioTableIndex)
            {
                DcTableId = componentConfig.DcTableSelector,
                AcTableId = componentConfig.AcTableSelector,
            };

            this.BlocksPerMcu += componentConfig.HorizontalSampleFactor * componentConfig.VerticalSampleFactor;
        }

        int maxSubFactorH = frameConfig.MaxHorizontalSamplingFactor;
        int maxSubFactorV = frameConfig.MaxVerticalSamplingFactor;
        this.McusPerLine = (int)Numerics.DivideCeil((uint)image.Width, (uint)maxSubFactorH * 8);
        this.McusPerColumn = (int)Numerics.DivideCeil((uint)image.Height, (uint)maxSubFactorV * 8);

        for (int i = 0; i < this.Components.Length; i++)
        {
            Component component = this.Components[i];
            component.Init(this, maxSubFactorH, maxSubFactorV);
        }
    }

    public JpegColorSpace ColorSpace { get; }

    public bool Interleaved { get; }

    public int PixelHeight { get; }

    public int PixelWidth { get; }

    public Component[] Components { get; }

    public int McusPerLine { get; }

    public int McusPerColumn { get; }

    public int BlocksPerMcu { get; }

    public void Dispose()
    {
        for (int i = 0; i < this.Components.Length; i++)
        {
            this.Components[i].Dispose();
        }
    }

    public void AllocateComponents(bool fullScan)
    {
        for (int i = 0; i < this.Components.Length; i++)
        {
            Component component = this.Components[i];
            component.AllocateSpectral(fullScan);
        }
    }
}
