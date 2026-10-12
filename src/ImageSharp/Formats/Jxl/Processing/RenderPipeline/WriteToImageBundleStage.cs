// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal class WriteToImageBundleStage(Configuration configuration, JxlImageBundle bundle, JxlColorEncoding colorEncoding)
    : RenderPipelineStageBase(configuration)
{
    public override string Name => "WriteIB";

    public override void SetInputSizes(Span<Size> inputSizes)
    {
        if (inputSizes.Length < 3)
        {
            throw new InvalidOperationException("Not enough input sizes");
        }

        for (int c = 1; c < inputSizes.Length; c++)
        {
            if (inputSizes[c].Width != inputSizes[0].Width ||
                inputSizes[c].Height != inputSizes[0].Height)
            {
                throw new InvalidOperationException("Input sizes must be equal to the first input size");
            }
        }

        JxlImage3F tmp = new(this.GetConfiguration(), inputSizes[0].Width, inputSizes[0].Height);

        if (!bundle.SetFromImage(tmp, colorEncoding))
        {
            throw new InvalidOperationException("Could not set the image of an image bundle");
        }

        foreach (JxlImageF ec in bundle.EnumerateExtraChannels())
        {
            // We don't want a memory leak here, as we're
            // removing old extra channels and inserting
            // new ones
            ec.Dispose();
        }

        bundle.ClearExtraChannels();

        for (int c = 3; c < inputSizes.Length; c++)
        {
            JxlImageF ch = new(this.GetConfiguration(), inputSizes[c].Width, inputSizes[c].Height);
            bundle.AddExtraChannel(ch);
        }
    }

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        if (xExtraLeft != 0 || xExtraRight != 0)
        {
            throw new InvalidOperationException("xExtraLeft and xExtraRight must be 0");
        }

        for (int c = 0; c < 3; c++)
        {
            this.GetInputRow(inputRows, c, 0)[..width]
                .CopyTo(bundle.Color!.PlaneRow(c, yPos)[xPos..]);
        }

        int ecIndex = 0;
        foreach (JxlImageF ec in bundle.EnumerateExtraChannels())
        {
            if (ec.XSize < xPos + width)
            {
                throw new InvalidOperationException("Width is too small");
            }

            this.GetInputRow(inputRows, ecIndex++ + 3, 0)[..width]
                .CopyTo(ec.GetRow(yPos)[(xPos - xExtraLeft)..]);
        }
    }

    public override RenderPipelineChannelMode GetChannelMode(int channel) => RenderPipelineChannelMode.Input;
}
