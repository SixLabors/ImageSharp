// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class IntrinsicFactAttribute : FactAttribute
    {
        public IntrinsicFactAttribute(_HwIntrinsics requiredIntrinsics)
        {
            _HwIntrinsics notSupported = requiredIntrinsics.GetNotSupportedIntrinsics();
            if (!IntrinsicTestsUtils.IntrinsicsSupported)
            {
                this.Skip = $"Current runtime does not support intrinsics";
            }
            else if (notSupported != _HwIntrinsics.None)
            {
                this.Skip = $"Required: {requiredIntrinsics}\nNot supported: {notSupported}";
            }
        }
    }
}
