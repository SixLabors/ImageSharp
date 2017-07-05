namespace ImageSharp.Tests
{
    using System;

    public static class TestEnvironment
    {
        private static Lazy<bool> runsOnCi = new Lazy<bool>(
            () =>
                {
                    bool isCi;
                    return bool.TryParse(Environment.GetEnvironmentVariable("CI"), out isCi) && isCi;
                });

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Gets a value indicating whether test execution runs on CI.
        /// </summary>
        internal static bool RunsOnCI => runsOnCi.Value;
    }
}