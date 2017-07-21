// <copyright file="FlagsHelper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using System.IO;

    using Xunit;
    using Xunit.Abstractions;
    
    public class TestEnvironmentTests
    {
        public TestEnvironmentTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }


        [Fact]
        public void GetSolutionDirectoryFullPath()
        {
            string path = TestEnvironment.GetSolutionDirectoryFullPath();
            this.Output.WriteLine(path);

            Assert.True(Directory.Exists(path));
        }

        [Fact]
        public void GetInputImagesDirectoryFullPath()
        {
            string path = TestEnvironment.GetInputImagesDirectoryFullPath();
            this.Output.WriteLine(path);

            Assert.True(Directory.Exists(path));
        }
    }
}
