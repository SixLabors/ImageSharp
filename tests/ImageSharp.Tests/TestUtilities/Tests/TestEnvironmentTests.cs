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
        
        private void CheckPath(string path)
        {
            this.Output.WriteLine(path);
            Assert.True(Directory.Exists(path));
        }

        [Fact]
        public void SolutionDirectoryFullPath()
        {
            this.CheckPath(TestEnvironment.SolutionDirectoryFullPath);
        }

        [Fact]
        public void InputImagesDirectoryFullPath()
        {
            this.CheckPath(TestEnvironment.InputImagesDirectoryFullPath);
        }

        [Fact]
        public void ActualOutputDirectoryFullPath()
        {
            this.CheckPath(TestEnvironment.ActualOutputDirectoryFullPath);
        }

        [Fact]
        public void ExpectedOutputDirectoryFullPath()
        {
            this.CheckPath(TestEnvironment.ReferenceOutputDirectoryFullPath);
        }

        [Fact]
        public void GetReferenceOutputFileName()
        {
            string actual = Path.Combine(TestEnvironment.ActualOutputDirectoryFullPath, @"foo\bar\lol.jpeg");
            string expected = TestEnvironment.GetReferenceOutputFileName(actual);

            this.Output.WriteLine(expected);
            Assert.Contains(TestEnvironment.ReferenceOutputDirectoryFullPath, expected);
        }
    }
}
