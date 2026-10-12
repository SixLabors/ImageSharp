// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.IO;

internal sealed class JxlCodecIO
{
    public JxlCodecIO()
    {
        this.PreviewFrame = new(this.CodecMetadata.ImageMetadata);
        this.Frames = [new(this.CodecMetadata.ImageMetadata)];
    }

    public JxlCodecMetadata CodecMetadata { get; } = new();

    public JxlImageBundle PreviewFrame { get; set; }

    public List<JxlImageBundle> Frames { get; } = [];

    public int LastStillFrame
    {
        get
        {
            int last = 0;

            for (int i = 0; i < this.Frames.Count; i++)
            {
                last = i;

                if (this.Frames[i].Duration > 0)
                {
                    break;
                }
            }

            return last;
        }
    }

    public JxlImageBundle Main => this.Frames[this.LastStillFrame];

    public int Width => this.CodecMetadata.Size!.XSize;

    public int Height => this.CodecMetadata.Size!.YSize;

    public void SetSize(int width, int height) => this.CodecMetadata.Size!.Set(width, height);

    public void ShrinkTo(int width, int height)
    {
        foreach (JxlImageBundle ib in this.Frames)
        {
            if (!ib.ShrinkTo(width, height))
            {
                throw new InvalidOperationException("Couldn't shrink an image bundle");
            }
        }

        this.SetSize(width, height);
    }
}
