// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.Tests
{
    internal class TestEnvironment
    {
        internal static bool Is64BitProcess => IntPtr.Size == 8;
    }
}