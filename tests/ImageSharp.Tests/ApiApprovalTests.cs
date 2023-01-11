// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Reflection;
using PublicApiGenerator;
using Shouldly;

namespace SixLabors.ImageSharp.Tests;

public class ApiApprovalTests
{
    [Fact]
    public void Public_api_should_not_change_unintentionally()
    {
        Assembly asmForTest = typeof(Image).Assembly;

        string publicApi = asmForTest.GeneratePublicApi(new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false,
            WhitelistedNamespacePrefixes = new[] { "Microsoft.Extensions.DependencyInjection" },
        });

        publicApi.ShouldMatchApproved(options =>
            options.WithFilenameGenerator(
                (testMethodInfo, discriminator, fileType, fileExtension) =>
                    $"{asmForTest.GetName().Name}.{fileType}.{fileExtension}"));
    }
}
