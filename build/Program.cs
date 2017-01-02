using Microsoft.DotNet.ProjectModel;
using System;
using System.IO;
using System.Linq;
using NuGet.Versioning;
using System.Collections.Generic;
using LibGit2Sharp;
using Newtonsoft.Json;
using System.Text;

namespace ConsoleApplication
{
    public class Program
    {
         const string fallbackTag = "CI";

        public static void Main(string[] args)
        {
            // TODO add options to updating the version number for indirvidual projects

            var resetmode = args.Contains("reset");

            // find the project root where glbal.json lives
            var root = ProjectRootResolver.ResolveRootDirectory(".");
            //lets find the repo
            var repo = new LibGit2Sharp.Repository(root);

            //lets find all the project.json files in the src folder (don't care about versioning `tests`)
            var projectFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), Project.FileName, SearchOption.AllDirectories);

            //open them and convert them to source projects
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
                //TODO force update of all dependent projects to point to the newest build.

                //we skip the build number and standard CI prefix on first commits
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

            //revert the project.json change be reverting it but skipp all the git stuff as its not needed
            foreach (var p in projects)
            {
                if (File.Exists($"{p.FullProjectFilePath}.bak"))
                {
                    File.Copy($"{p.FullProjectFilePath}.bak", p.FullProjectFilePath, true);
                    File.Delete($"{p.FullProjectFilePath}.bak");
                }
            }
        }
        
        public class SourceProject
        {
            private readonly IEnumerable<string> dependencies;

            public string ProjectDirectory { get; }

            public NuGetVersion Version { get; }

            public List<SourceProject> DependentProjects { get; private set; }
            public string Name { get; private set; }
            public string ProjectFilePath { get; private set; }

            public int CommitCountSinceVersionChange { get; private set; } = 0;
            public string FullProjectFilePath { get; private set; }
            public string FinalVersionNumber { get; private set; }

            public SourceProject(Project project, string root)
            {
                this.Name = project.Name;
                this.ProjectDirectory = project.ProjectDirectory.Substring(root.Length);
                this.ProjectFilePath = project.ProjectFilePath.Substring(root.Length);
                this.FullProjectFilePath = project.ProjectFilePath;
                this.Version = project.Version;
                this.dependencies = project.Dependencies.Select(x => x.Name);
                this.FinalVersionNumber = Version.ToFullString();
            }

            public void PopulateDependencies(IEnumerable<SourceProject> projects)
            {
                DependentProjects = projects.Where(x => dependencies.Contains(x.Name)).ToList();
                
            }

            private bool MatchPath(string path)
            {
                if(path.StartsWith(this.ProjectDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (DependentProjects.Any())
                {
                    return DependentProjects.Any(x => x.MatchPath(path));
                }
                return false;
            }

            private bool ApplyCommitInternal(Commit commit, TreeChanges changes, Repository repo)
            {
                CommitCountSinceVersionChange++;

                //return false if this is a version number root
                var projectFileChange = changes.Where(x => x.Path?.Equals(this.ProjectFilePath, StringComparison.OrdinalIgnoreCase) == true).FirstOrDefault();
                if(projectFileChange != null)
                {
                    if(projectFileChange.Status == ChangeKind.Added)
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
                            if(project.Version != this.Version)
                            {
                                //version changed
                                return false;
                            }
                        }
                    }

                    // version must have been the same lets carry on
                    return true;
                }

                return true;
            }

            internal void CalculateVersion(Repository repo, string branch)
            {
                foreach(var c in repo.Commits)
                {
                    if(!ApplyCommit(c, repo))
                    {

                        //we have finished lets populate the final version number
                        this.FinalVersionNumber = CalculateVersionNumber(branch);

                        return;
                    }
                }
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
                            if (MatchPath(change.OldPath))
                            {
                                return ApplyCommitInternal(commit, changes, repo);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(change.Path))
                        {
                            if (MatchPath(change.Path))
                            {
                                return ApplyCommitInternal(commit, changes, repo);
                            }
                        }
                    }
                }

                return true;
            }

            private string CalculateVersionNumber(string branch)
            {
                var version = this.Version.ToFullString();
                
                if (this.CommitCountSinceVersionChange == 1 && branch == "master") //master only
                {
                    if (this.Version.IsPrerelease)
                    {
                        //prerelease always needs the build counter just not on a branch name
                        return $"{version}-{this.CommitCountSinceVersionChange:00000}";
                    }
                   
                    // this is the full release happy path, first commit after changing the version number
                    return version;
                }

                var rootSpecialVersion = "";

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
                        branch = fallbackTag;
                    }else
                    {
                        branch = "";
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
                
                if(branch.Length > maxLength)
                {
                    branch = branch.Substring(0, maxLength);
                }
                
                return $"{version}{rootSpecialVersion}{branch}-{this.CommitCountSinceVersionChange:00000}";
            }
        }
    }
}
