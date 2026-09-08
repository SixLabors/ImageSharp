// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

internal enum JpegSerializationStage
{
    Initialize,
    SerializeSection,
    Done,
    Error
}
