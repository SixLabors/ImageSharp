// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

// Ensure the internals can be tested.
[assembly: InternalsVisibleTo("SixLabors.Core.Tests")]

// Ensure the internals are visible to the other projects.
[assembly: InternalsVisibleTo("SixLabors.Exif")]
[assembly: InternalsVisibleTo("SixLabors.Fonts")]
[assembly: InternalsVisibleTo("SixLabors.ImageSharp")]
[assembly: InternalsVisibleTo("SixLabors.ImageSharp.Drawing")]
[assembly: InternalsVisibleTo("SixLabors.Shapes")]
[assembly: InternalsVisibleTo("SixLabors.Shapes.Text")]