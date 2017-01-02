// <copyright file="Program.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ConsoleApplication
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    using LibGit2Sharp;
    using Microsoft.DotNet.ProjectModel;
    using Newtonsoft.Json;
    using NuGet.Versioning;

    /// <summary>
    /// This updates the version numbers for all the projects in the src folder.
    /// The version number it will geneate is dependent on if this is a build from master or a branch/PR
    ///
    /// If its a build on master
    /// We take the version number specified in project.json,
    /// count how meny commits the repo has had that will affect this project or its dependencies since the version number of manually changed
    /// If this is the first commit that effected this project since number change then leave the version number as defined i.e. will build 1.0.0 if thats in project.json
    /// unless it is a preview build number in which case we always add the counter
    ///
    /// If the build is from a PR/branch
    /// We take the version number specified in project.json, append a tag for the branch/PR (so we can determin how each package was built)
    /// append number of commits effecting the project.
    ///
    /// </summary>
    /// <example>
    /// for PR#123 and project.json version 2.0.1 and we have had 30 commits affecting the project
    /// we would end up with version number 2.0.1-PR124-00030
    ///
    /// for branch `fix-stuff` project.json version 2.0.1-alpha1 and we have had 832 commits affecting the project
    /// we would end up with version number 2.0.1-alpha1-fix-stuff-00832
    ///
    /// for `master` project.json version 2.0.1-alpha1 and we have had 832 commits affecting the project
    /// we would end up with version number 2.0.1-alpha1-00832
    ///
    /// for `master` project.json version 2.0.1 and we have had 132 commits affecting the project
    /// we would end up with version number 2.0.1-CI-00132
    ///
    /// for `master` project.json version 2.0.1 and we have had 1 commits affecting the project
    /// we would end up with version number 2.0.1
    ///
    /// for `master` project.json version 2.0.1-alpha1 and we have had 1 commits affecting the project
    /// we would end up with version number 2.0.1-alpha1
    /// </example>
    /// <remarks>
    /// TODO Add the option for using this to update the version numbers in a project and its dependent references.
    /// </remarks>
    public class Program
    {
        private const string FallbackTag = "CI";

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">The arguments.</param>
        public static void Main(string[] args)
        {
            var resetmode = args.Contains("reset");

            // Find the project root where glbal.json lives
            var root = ProjectRootResolver.ResolveRootDirectory(".");

            // Lets find the repo
            var repo = new LibGit2Sharp.Repository(root);

            // Lets find all the project.json files in the src folder (don't care about versioning `tests`)
            var projectFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), Project.FileName, SearchOption.AllDirectories);

            // Open them and convert them to source projects
            var projects = projectFiles.Select(x => ProjectReader.GetProject(x))
                            .Select(x => new SourceProject(x, repo.Info.WorkingDirectory))
                            .ToList();

            if (resetmode)
            {
                ResetProject(projects);
            }
            else
            {
                CaclulateProjectVersionNumber(projects, repo);

                UpdateVersionNumbers(projects);

                CreateBuildScript(projects);

                foreach (var p in projects)
                {
                    Console.WriteLine($"{p.Name} {p.FinalVersionNumber}");
                }
            }
        }

        private static void CreateBuildScript(IEnumerable<SourceProject> projects)
        {
            var sb = new StringBuilder();
            foreach (var p in projects)
            {
                sb.AppendLine($@"dotnet pack --configuration Release --output ""artifacts\bin\ImageSharp"" ""{p.ProjectFilePath}""");
            }

            File.WriteAllText("build-inner.cmd", sb.ToString());
        }

        private static void UpdateVersionNumbers(IEnumerable<SourceProject> projects)
        {
            foreach (var p in projects)
            {
                // TODO force update of all dependent projects to point to the newest build.
                // we skip the build number and standard CI prefix on first commits
                var newVersion = p.FinalVersionNumber;

                // create a backup file so we can rollback later without breaking formatting
                File.Copy(p.FullProjectFilePath, $"{p.FullProjectFilePath}.bak", true);

                dynamic projectFile = JsonConvert.DeserializeObject(File.ReadAllText(p.FullProjectFilePath));

                projectFile.version = $"{newVersion}-*";
                File.WriteAllText(p.FullProjectFilePath, JsonConvert.SerializeObject(projectFile, Formatting.Indented));
            }
        }

        private static string CurrentBranch(Repository repo)
        {
            // lets build version friendly commit
            string branch = repo.Head.FriendlyName;

            // lets see if we are running in appveyor and if we are use the environment variables instead of the head
            var appveryorBranch = Environment.GetEnvironmentVariable("APPVEYOR_REPO_BRANCH");
            if (!string.IsNullOrWhiteSpace(appveryorBranch))
            {
                branch = appveryorBranch;
            }

            var prNumber = Environment.GetEnvironmentVariable("APPVEYOR_PULL_REQUEST_NUMBER");
            if (!string.IsNullOrWhiteSpace(prNumber))
            {
                branch = $"PR{int.Parse(prNumber):000}";
            }

            // this will happen when checking out a comit directly and not a branch (like appveryor does when it builds)
            if (branch == "(no branch)")
            {
                throw new Exception("unable to find branch");
            }

            // clean branch names (might need to be improved)
            branch = branch.Replace("/", "-").Replace("--", "-");

            return branch;
        }

        private static void CaclulateProjectVersionNumber(List<SourceProject> projects, Repository repo)
        {
            var branch = CurrentBranch(repo);

            // populate the dependency chains
            projects.ForEach(x => x.PopulateDependencies(projects));

            // update the final version based on the repo history and the currentr branch name
            projects.ForEach(x => x.CalculateVersion(repo, branch));
        }

        private static void ResetProject(List<SourceProject> projects)
        {
            if (File.Exists("build-inner.bak"))
            {
                File.Copy("build-inner.bak", "build-inner.cmd", true);
                File.Delete("build-inner.bak");
            }

            // revert the project.json change be reverting it but skipp all the git stuff as its not needed
            foreach (var p in projects)
            {
                if (File.Exists($"{p.FullProjectFilePath}.bak"))
                {
                    File.Copy($"{p.FullProjectFilePath}.bak", p.FullProjectFilePath, true);
                    File.Delete($"{p.FullProjectFilePath}.bak");
                }
            }
        }

        /// <summary>
        /// Project level logic
        /// </summary>
        public class SourceProject
        {
            private readonly IEnumerable<string> dependencies;

            /// <summary>
            /// Initializes a new instance of the <see cref="SourceProject"/> class.
            /// </summary>
            /// <param name="project">The project.</param>
            /// <param name="root">The root.</param>
            public SourceProject(Project project, string root)
            {
                this.Name = project.Name;
                this.ProjectDirectory = project.ProjectDirectory.Substring(root.Length);
                this.ProjectFilePath = project.ProjectFilePath.Substring(root.Length);
                this.FullProjectFilePath = project.ProjectFilePath;
                this.Version = project.Version;
                this.dependencies = project.Dependencies.Select(x => x.Name);
                this.FinalVersionNumber = this.Version.ToFullString();
            }

            /// <summary>
            /// Gets the project directory.
            /// </summary>
            /// <value>
            /// The project directory.
            /// </value>
            public string ProjectDirectory { get; }

            /// <summary>
            /// Gets the version.
            /// </summary>
            /// <value>
            /// The version.
            /// </value>
            public NuGetVersion Version { get; }

            /// <summary>
            /// Gets the dependent projects.
            /// </summary>
            /// <value>
            /// The dependent projects.
            /// </value>
            public List<SourceProject> DependentProjects { get; private set; }

            /// <summary>
            /// Gets the name.
            /// </summary>
            /// <value>
            /// The name.
            /// </value>
            public string Name { get; private set; }

            /// <summary>
            /// Gets the project file path.
            /// </summary>
            /// <value>
            /// The project file path.
            /// </value>
            public string ProjectFilePath { get; private set; }

            /// <summary>
            /// Gets the commit count since version change.
            /// </summary>
            /// <value>
            /// The commit count since version change.
            /// </value>
            public int CommitCountSinceVersionChange { get; private set; } = 0;

            /// <summary>
            /// Gets the full project file path.
            /// </summary>
            /// <value>
            /// The full project file path.
            /// </value>
            public string FullProjectFilePath { get; private set; }

            /// <summary>
            /// Gets the final version number.
            /// </summary>
            /// <value>
            /// The final version number.
            /// </value>
            public string FinalVersionNumber { get; private set; }

            /// <summary>
            /// Populates the dependencies.
            /// </summary>
            /// <param name="projects">The projects.</param>
            public void PopulateDependencies(IEnumerable<SourceProject> projects)
            {
                this.DependentProjects = projects.Where(x => this.dependencies.Contains(x.Name)).ToList();
            }

            /// <summary>
            /// Calculates the version.
            /// </summary>
            /// <param name="repo">The repo.</param>
            /// <param name="branch">The branch.</param>
            internal void CalculateVersion(Repository repo, string branch)
            {
                foreach (var c in repo.Commits)
                {
                    if (!this.ApplyCommit(c, repo))
                    {
                        // we have finished lets populate the final version number
                        this.FinalVersionNumber = this.CalculateVersionNumber(branch);

                        return;
                    }
                }
            }

            private bool MatchPath(string path)
            {
                if (path.StartsWith(this.ProjectDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (this.DependentProjects.Any())
                {
                    return this.DependentProjects.Any(x => x.MatchPath(path));
                }

                return false;
            }

            private bool ApplyCommitInternal(Commit commit, TreeChanges changes, Repository repo)
            {
                this.CommitCountSinceVersionChange++;

                // return false if this is a version number root
                var projectFileChange = changes.Where(x => x.Path?.Equals(this.ProjectFilePath, StringComparison.OrdinalIgnoreCase) == true).FirstOrDefault();
                if (projectFileChange != null)
                {
                    if (projectFileChange.Status == ChangeKind.Added)
                    {
                        // the version must have been set here
                        return false;
                    }
                    else
                    {
                        var blob = repo.Lookup<Blob>(projectFileChange.Oid);
                        using (var s = blob.GetContentStream())
                        {
                            var project = new ProjectReader().ReadProject(s, this.Name, this.FullProjectFilePath, null);
                            if (project.Version != this.Version)
                            {
                                // version changed
                                return false;
                            }
                        }
                    }

                    // version must have been the same lets carry on
                    return true;
                }

                return true;
            }

            private bool ApplyCommit(Commit commit, Repository repo)
            {
                foreach (var parent in commit.Parents)
                {
                    var changes = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);

                    foreach (TreeEntryChanges change in changes)
                    {
                        if (!string.IsNullOrWhiteSpace(change.OldPath))
                        {
                            if (this.MatchPath(change.OldPath))
                            {
                                return this.ApplyCommitInternal(commit, changes, repo);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(change.Path))
                        {
                            if (this.MatchPath(change.Path))
                            {
                                return this.ApplyCommitInternal(commit, changes, repo);
                            }
                        }
                    }
                }

                return true;
            }

            private string CalculateVersionNumber(string branch)
            {
                var version = this.Version.ToFullString();

                // master only
                if (this.CommitCountSinceVersionChange == 1 && branch == "master")
                {
                    if (this.Version.IsPrerelease)
                    {
                        // prerelease always needs the build counter just not on a branch name
                        return $"{version}-{this.CommitCountSinceVersionChange:00000}";
                    }

                    // this is the full release happy path, first commit after changing the version number
                    return version;
                }

                var rootSpecialVersion = string.Empty;

                if (this.Version.IsPrerelease)
                {
                    // probably a much easy way for doing this but it work sell enough for a build script
                    var parts = version.Split(new[] { '-' }, 2);
                    version = parts[0];
                    rootSpecialVersion = parts[1];
                }

                // if master and the version doesn't manually specify a prerelease tag force one on for CI builds
                if (branch == "master")
                {
                    if (!this.Version.IsPrerelease)
                    {
                        branch = FallbackTag;
                    }
                    else
                    {
                        branch = string.Empty;
                    }
                }

                if (rootSpecialVersion.Length > 0)
                {
                    rootSpecialVersion = "-" + rootSpecialVersion;
                }

                if (branch.Length > 0)
                {
                    branch = "-" + branch;
                }

                var maxLength = 20; // dotnet will fail to populate the package if the tag is > 20
                maxLength -= rootSpecialVersion.Length; // this is a required tag
                maxLength -= 7; // for the counter and dashes

                if (branch.Length > maxLength)
                {
                    branch = branch.Substring(0, maxLength);
                }

                return $"{version}{rootSpecialVersion}{branch}-{this.CommitCountSinceVersionChange:00000}";
            }
        }
    }
}
