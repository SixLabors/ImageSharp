// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// Implementation of the Color Management System.
/// </summary>
// TODO
internal class JxlCmsImpl
{
    public Memory<byte> IccSource { get; set; }

    public Memory<byte> IccDestination { get; set; }

    public bool ApplyHlgOotf { get; set; }

    public int HlgOotfNumChannels { get; set; }

    public float[] HlgOotfLuminances { get; set; } = new float[3];

    public int ChannelsSource { get; set; }

    public int ChannelsDestination { get; set; }

    public List<float> SourceStorage { get; set; } = [];

    public List<Memory<float>> BufferSource { get; set; } = [];

    public List<float> DestinationStorage { get; set; } = [];

    public List<Memory<float>> BufferDestination { get; set; } = [];

    public float IntensityTarget { get; set; }

    public bool SkipLCms { get; set; }

    public JxlExtraTransferFunction PreProcess { get; set; } = JxlExtraTransferFunction.None;

    public JxlExtraTransferFunction PostProcess { get; set; } = JxlExtraTransferFunction.None;
}
