// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.XUnitExtensions;
using XUnit;

namespace Xunit;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ConditionalFactAttribute : FactAttribute
{
    [DynamicallyAccessedMembers(StaticReflectionConstants.ConditionalMemberKinds)]
    public Type CalleeType { get; private set; }

    public string[] ConditionMemberNames { get; private set; }

    public ConditionalFactAttribute(
        [DynamicallyAccessedMembers(StaticReflectionConstants.ConditionalMemberKinds)]
        Type calleeType,
        params string[] conditionMemberNames)
    {
        this.CalleeType = calleeType;
        this.ConditionMemberNames = conditionMemberNames;
        string skipReason = ConditionalTestDiscoverer.EvaluateSkipConditions(calleeType, conditionMemberNames);
        if (skipReason != null)
        {
            this.Skip = skipReason;
        }
    }

    [Obsolete(
        "Use the overload that takes a Type parameter: ConditionalFact(typeof(MyClass), nameof(MyCondition)).")]
    public ConditionalFactAttribute(params string[] conditionMemberNames) => this.ConditionMemberNames = conditionMemberNames;
}
