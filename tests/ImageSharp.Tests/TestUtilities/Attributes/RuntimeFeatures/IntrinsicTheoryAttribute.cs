// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class IntrinsicTheoryAttribute : TheoryAttribute
    {
        public IntrinsicTheoryAttribute(_HwIntrinsics requiredIntrinsics)
        {
            _HwIntrinsics notSupported = requiredIntrinsics.GetNotSupportedIntrinsics();
            if (notSupported != _HwIntrinsics.None)
            {
                this.Skip = $"Required: {requiredIntrinsics}\nNot supported: {notSupported}";
            }
        }
    }
}
