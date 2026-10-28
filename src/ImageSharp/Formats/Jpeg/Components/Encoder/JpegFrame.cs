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
    /// <summary>
    /// Initializes a new instance of the <see cref="JpegFrame"/> class.
    /// </summary>
    /// <param name="configuration">The memory allocation configuration.</param>
    /// <param name="size">The encoded pixel size.</param>
    /// <param name="frameConfig">The JPEG component layout.</param>
    /// <param name="interleaved">Whether scans interleave their components.</param>
    public JpegFrame(Configuration configuration, Size size, JpegFrameConfig frameConfig, bool interleaved)
    {
        this.ColorSpace = frameConfig.ColorType;

        this.Interleaved = interleaved;

        this.PixelWidth = size.Width;
        this.PixelHeight = size.Height;

        MemoryAllocator allocator = configuration.MemoryAllocator;

        JpegComponentConfig[] componentConfigs = frameConfig.Components;
        this.Components = new Component[componentConfigs.Length];
        for (int i = 0; i < this.Components.Length; i++)
        {
            JpegComponentConfig componentConfig = componentConfigs[i];
            this.Components[i] = new Component(
                allocator,
                componentConfig.HorizontalSampleFactor,
                componentConfig.VerticalSampleFactor,
                componentConfig.QuantizatioTableIndex)
            {
                DcTableId = componentConfig.DcTableSelector,
                AcTableId = componentConfig.AcTableSelector,
            };

            this.BlocksPerMcu += componentConfig.HorizontalSampleFactor * componentConfig.VerticalSampleFactor;
        }

        int maxSubFactorH = frameConfig.MaxHorizontalSamplingFactor;
        int maxSubFactorV = frameConfig.MaxVerticalSamplingFactor;
        this.McusPerLine = (int)Numerics.DivideCeil((uint)size.Width, (uint)maxSubFactorH * 8);
        this.McusPerColumn = (int)Numerics.DivideCeil((uint)size.Height, (uint)maxSubFactorV * 8);

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
