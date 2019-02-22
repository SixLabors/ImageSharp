// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Processors.Convolution;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution
{
    public class BokehBlurTest : FileTestBase
    {
        private static readonly string Components10x2 =@"
        [[ 0.00451261+0.0165137j   0.02161237-0.00299122j  0.00387479-0.02682816j
          -0.02752798-0.01788438j -0.03553877+0.0154543j  -0.01428268+0.04224722j
           0.01747482+0.04687464j  0.04243676+0.03451751j  0.05564306+0.01742537j
           0.06040984+0.00459225j  0.06136251+0.0j         0.06040984+0.00459225j
           0.05564306+0.01742537j  0.04243676+0.03451751j  0.01747482+0.04687464j
          -0.01428268+0.04224722j -0.03553877+0.0154543j  -0.02752798-0.01788438j
           0.00387479-0.02682816j  0.02161237-0.00299122j  0.00451261+0.0165137j ]]
        [[-0.00227282+0.002851j   -0.00152245+0.00604545j  0.00135338+0.00998296j
           0.00698622+0.01370844j  0.0153483+0.01605112j   0.02565295+0.01611732j
           0.03656958+0.01372368j  0.04662725+0.00954624j  0.05458942+0.00491277j
           0.05963937+0.00133843j  0.06136251+0.0j         0.05963937+0.00133843j
           0.05458942+0.00491277j  0.04662725+0.00954624j  0.03656958+0.01372368j
           0.02565295+0.01611732j  0.0153483+0.01605112j   0.00698622+0.01370844j
           0.00135338+0.00998296j -0.00152245+0.00604545j -0.00227282+0.002851j  ]]";

        [Fact]
        public void VerifyComplexComponents()
        {
            // Get the saved components
            var components = new List<Complex64[]>();
            foreach (Match match in Regex.Matches(Components10x2, @"\[\[(.*?)\]\]", RegexOptions.Singleline))
            {
                string[] values = match.Groups[1].Value.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Complex64[] component = values.Select(
                    value =>
                        {
                            Match pair = Regex.Match(value.Replace('.', ','), @"([+-]?\d+,\d+)([+-]?\d+,\d+)j");
                            return new Complex64(float.Parse(pair.Groups[1].Value), float.Parse(pair.Groups[2].Value));
                        }).ToArray();
                components.Add(component);
            }

            // Make sure the kernel components are the same
            var processor = new BokehBlurProcessor<Rgb24>(10);
            Assert.Equal(components.Count, processor.Kernels.Count);
            foreach ((Complex64[] a, DenseMatrix<Complex64> b) in components.Zip(processor.Kernels, (a, b) => (a, b)))
            {
                Span<Complex64> spanA = a.AsSpan(), spanB = b.Span;
                Assert.Equal(spanA.Length, spanB.Length);
                for (int i = 0; i < spanA.Length; i++)
                {
                    Assert.True(Math.Abs(Math.Abs(spanA[i].Real) - Math.Abs(spanB[i].Real)) < 0.00000001f);
                    Assert.True(Math.Abs(Math.Abs(spanA[i].Imaginary) - Math.Abs(spanB[i].Imaginary)) < 0.00000001f);
                }
            }
        }
    }
}
