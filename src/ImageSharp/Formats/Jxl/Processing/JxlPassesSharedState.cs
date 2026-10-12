// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable SA1401 // Fields should be private

using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal class JxlPassesSharedState
{
    // Make this a field so we can get a ref to it
    public JxlBlockContextMap BlockContextMap;

    public JxlCodecMetadata CodecMetadata { get; set; } = new();

    public JxlFrameDimensions FrameDimensions { get; set; }

    public JxlAcStrategyImage AcStrategy { get; set; }

    public JxlDequantMatrices Matrices { get; set; } = new();

    public JxlQuantizer Quantizer { get; set; }

    public JxlImageI RawQuantField { get; set; }

    public JxlImageB EpfSharpness { get; set; }

    public JxlColorCorrelationMap ColorMap { get; set; }

    public JxlImageFeatures ImageFeatures { get; set; } = new();

    public int CoeffOrderSize { get; set; }

    public List<byte> CoeffOrders { get; set; } = [];

    public JxlImageB QuantDc { get; set; }

    public JxlImage3F DcStorage { get; set; }

    public JxlImage3F Dc { get; set; }

    public JxlImage3F[] DcFrames { get; set; } = new JxlImage3F[4];

    public JxlReferenceFrame[] ReferenceFrames { get; set; } = new JxlReferenceFrame[4];

    public int NumHistograms { get; set; }

    public JxlPassesSharedState(Configuration configuration, JxlFrameHeader frameHeader, bool encoder)
    {
        if (frameHeader.Metadata is null)
        {
            throw new InvalidOperationException("The frame header metadata is missing");
        }

        this.CodecMetadata = frameHeader.Metadata;
        this.FrameDimensions = frameHeader.FrameDimensions;
        this.ImageFeatures.PatchDictionary.SetShared(this.ImageFeatures.ReferenceFrames);

        JxlFrameDimensions dimensions = frameHeader.FrameDimensions;

        this.AcStrategy = JxlAcStrategyImage.Create(configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
        this.RawQuantField = new JxlImageI(configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
        this.EpfSharpness = new JxlImageB(configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
        this.ColorMap = JxlColorCorrelationMap.Create(configuration, dimensions.XSize, dimensions.YSize);

        this.CoeffOrderSize = JxlCoefficientOrder.CoefficientOrderMaxSize;

        if (encoder &&
            this.CoeffOrders.Count < (frameHeader.Passes.NumPasses & JxlCoefficientOrder.CoefficientOrderMaxSize) &&
            frameHeader.Encoding == JxlFrameEncoding.VarDct)
        {
            // we add the padding to CoeffOrders so its length is equal to the variable upperBound
            int upperBound = frameHeader.Passes.NumPasses & JxlCoefficientOrder.CoefficientOrderMaxSize;
            int length = this.CoeffOrders.Count;
            int delta = upperBound - length;

            for (int i = 0; i < delta; i++)
            {
                this.CoeffOrders.Add(0); // default constant
            }
        }

        this.QuantDc = new JxlImageB(configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);

        bool useDcFrame = (frameHeader.Flags & (ulong)JxlFrameHeaderFlags.Dc) != 0;
        if (!encoder && useDcFrame)
        {
            if (frameHeader.DcLevel == 4)
            {
                throw new InvalidOperationException("DC level for DC frames cannot be equal to 4");
            }

            this.DcStorage = new JxlImage3F();
            this.Dc = this.DcFrames[(int)frameHeader.DcLevel];

            if (this.Dc.XSize == 0)
            {
                throw new InvalidOperationException("DC frame was specified for DC Level = " + frameHeader.DcLevel + ", but frame wasn't decoded with level " + frameHeader.DcLevel + 1);
            }

            this.QuantDc.Clear();
        }
        else
        {
            this.DcStorage = new JxlImage3F(configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
            this.Dc = this.DcStorage;
        }

        this.Quantizer = new(this.Matrices);
    }
}
