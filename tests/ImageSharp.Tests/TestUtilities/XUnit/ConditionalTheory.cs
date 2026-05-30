// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.XUnitExtensions;
using Xunit.Sdk;

namespace Xunit
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ConditionalTheoryAttribute : TheoryAttribute
    {
        [DynamicallyAccessedMembers(StaticReflectionConstants.ConditionalMemberKinds)]
        public Type CalleeType { get; private set; }
        public string[] ConditionMemberNames { get; private set; }

        public ConditionalTheoryAttribute(
            [DynamicallyAccessedMembers(StaticReflectionConstants.ConditionalMemberKinds)]
                Type calleeType,
            params string[] conditionMemberNames
        )
        {
            CalleeType = calleeType;
            ConditionMemberNames = conditionMemberNames;
            string skipReason = ConditionalTestDiscoverer.EvaluateSkipConditions(
                calleeType,
                conditionMemberNames
            );
            if (skipReason != null)
                Skip = skipReason;
        }

        [Obsolete(
            "Use the overload that takes a Type parameter: ConditionalTheory(typeof(MyClass), nameof(MyCondition))."
        )]
        public ConditionalTheoryAttribute(params string[] conditionMemberNames)
        {
            ConditionMemberNames = conditionMemberNames;
        }
    }
}
