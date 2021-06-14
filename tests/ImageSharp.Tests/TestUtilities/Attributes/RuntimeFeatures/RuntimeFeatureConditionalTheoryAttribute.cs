// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RuntimeFeatureConditionalTheoryAttribute : TheoryAttribute
    {
        public RuntimeFeatureConditionalTheoryAttribute(RuntimeFeature requiredIntrinsics)
        {
            RuntimeFeature notSupported = requiredIntrinsics.GetNotSupportedIntrinsics();
            if (notSupported != RuntimeFeature.None)
            {
                this.Skip = $"Required: {requiredIntrinsics}\nNot supported: {notSupported}";
            }
        }
    }
}
