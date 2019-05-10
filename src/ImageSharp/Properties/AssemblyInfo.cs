// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

// Ensure the other projects can see the internal helpers
[assembly: InternalsVisibleTo("SixLabors.ImageSharp.Drawing")]

// Redundant suppressing of SA1413 for Rider.
[assembly:
    System.Diagnostics.CodeAnalysis.SuppressMessage(
        "StyleCop.CSharp.MaintainabilityRules",
        "SA1413:UseTrailingCommasInMultiLineInitializers",
        Justification = "Follows SixLabors.ruleset")]