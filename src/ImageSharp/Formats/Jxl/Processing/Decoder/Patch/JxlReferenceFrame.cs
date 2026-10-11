// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Patch;

internal record JxlReferenceFrame(JxlImageBundle Frame, bool ImageBundleIsInXyb = false);
