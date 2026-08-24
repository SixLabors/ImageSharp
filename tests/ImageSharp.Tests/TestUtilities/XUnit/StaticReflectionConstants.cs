// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace XUnit;

internal static class StaticReflectionConstants
{
    // ConditionalTestDiscoverer looks at all fields/methods/properties recursively.
    public const DynamicallyAccessedMemberTypes ConditionalMemberKinds =
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.PublicProperties;
}
