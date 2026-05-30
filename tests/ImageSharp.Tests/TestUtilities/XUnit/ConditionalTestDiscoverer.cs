// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Xunit.Sdk;

namespace Microsoft.DotNet.XUnitExtensions
{
    // Internal helper class for code common to conditional test discovery through
    // [ConditionalFact] and [ConditionalTheory]
    internal static class ConditionalTestDiscoverer
    {
        /// <summary>
        /// Evaluates skip conditions given an explicit callee type and condition member names.
        /// Used by attribute constructors in xunit v3 where discoverers are not needed.
        /// </summary>
        internal static string EvaluateSkipConditions(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type calleeType,
            string[] conditionMemberNames
        )
        {
            if (
                calleeType == null
                || conditionMemberNames == null
                || conditionMemberNames.Length == 0
            )
                return null;

            List<string> falseConditions = new(conditionMemberNames.Length);
            foreach (string entry in conditionMemberNames)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                Func<bool> conditionFunc = LookupConditionalMember(calleeType, entry);
                if (conditionFunc == null)
                    throw new ConditionalDiscovererException(
                        GetFailedLookupString(entry, calleeType)
                    );

                try
                {
                    if (!conditionFunc())
                        falseConditions.Add(entry);
                }
                catch (Exception exc)
                {
                    falseConditions.Add($"{entry} ({exc.GetType().Name})");
                }
            }

            return falseConditions.Count > 0
                ? string.Format(
                    "Condition(s) not met: \"{0}\"",
                    string.Join("\", \"", falseConditions)
                )
                : null;
        }

        internal static string GetFailedLookupString(string name, Type type)
        {
            return $"An appropriate member '{name}' could not be found. "
                + $"The conditional method needs to be a static method, property, or field on the type {type} or any ancestor, "
                + "of any visibility, accepting zero arguments, and having a return type of Boolean.";
        }

        internal static Func<bool> LookupConditionalMember(Type t, string name)
        {
            if (t == null || name == null)
                return null;

            TypeInfo ti = t.GetTypeInfo();

            MethodInfo mi = ti.GetDeclaredMethod(name);
            if (
                mi != null
                && mi.IsStatic
                && mi.GetParameters().Length == 0
                && mi.ReturnType == typeof(bool)
            )
                return () => (bool)mi.Invoke(null, null);

            PropertyInfo pi = ti.GetDeclaredProperty(name);
            if (
                pi != null
                && pi.PropertyType == typeof(bool)
                && pi.GetMethod != null
                && pi.GetMethod.IsStatic
                && pi.GetMethod.GetParameters().Length == 0
            )
                return () => (bool)pi.GetValue(null);

            FieldInfo fi = ti.GetDeclaredField(name);
            if (fi != null && fi.FieldType == typeof(bool) && fi.IsStatic)
                return () => (bool)fi.GetValue(null);

            return LookupConditionalMember(ti.BaseType, name);
        }

        internal static bool CheckInputToSkipExecution(
            object[] conditionArguments,
            ref Type calleeType,
            ref string[] conditionMemberNames,
            object testMethod = null
        )
        {
            // A null or empty list of conditionArguments is treated as "no conditions".
            // and the test cases will be executed.
            // Example: [ConditionalClass()]
            if (conditionArguments == null || conditionArguments.Length == 0)
                return true;

            calleeType = conditionArguments[0] as Type;
            if (calleeType != null)
            {
                if (conditionArguments.Length < 2)
                {
                    // [ConditionalFact(typeof(x))] no provided methods.
                    return true;
                }

                // [ConditionalFact(typeof(x), "MethodName")]
                conditionMemberNames = conditionArguments[1] as string[];
            }
            else
            {
                // For [ConditionalClass], unable to get the Type info. All test cases will be executed.
                if (testMethod == null)
                    return true;

                // [ConditionalFact("MethodName")]
                conditionMemberNames = conditionArguments[0] as string[];
            }

            // [ConditionalFact((string[]) null)]
            if (conditionMemberNames == null || conditionMemberNames.Count() == 0)
                return true;

            return false;
        }
    }
}
