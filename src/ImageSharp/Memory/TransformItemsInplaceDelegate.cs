﻿// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Memory;

internal delegate void TransformItemsInplaceDelegate<T>(Span<T> data);
