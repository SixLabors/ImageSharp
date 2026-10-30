// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal class WriteToImage3FStage(Configuration configuration, JxlImage3<float> image)
    : RenderPipelineStageBase(configuration)
{
    /// <inheritdoc />
    public override string Name => "WriteI3F";

    /// <inheritdoc />
    public override void SetInputSizes(Span<Size> inputSizes)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(inputSizes.Length, 3, nameof(inputSizes));

        for (int c = 1; c < 3; c++)
        {
            // Each channel must have equal size.
            if (inputSizes[c].Width != inputSizes[0].Width ||
                inputSizes[c].Height != inputSizes[0].Height)
            {
                throw new InvalidOperationException("All input sizes must be equal to the first input size");
            }
        }

        // Create a new image with appropriate size.
        image.Dispose();
        image = new(this.GetConfiguration(), inputSizes[0].Width, inputSizes[0].Height);
    }

    /// <inheritdoc />
    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        // We don't need any extra width.
        if (xExtraLeft != 0 || xExtraRight != 0)
        {
            throw new InvalidOperationException("xExtraLeft and xExtraRight must be 0");
        }

        // Copy the rows from input to the image specified in the constructor.
        for (int c = 0; c < 3; c++)
        {
            this.GetInputRow(inputRows, c, 0)[..width]
                .CopyTo(image.PlaneRow(c, yPos)[xPos..]);
        }
    }

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel)
        // This stage is read-only and needs exactly 3 channels.
        // When there are not enough channels, the stage should not run.
        => channel < 3 ? RenderPipelineChannelMode.Input
                       : RenderPipelineChannelMode.Ignored;
}
