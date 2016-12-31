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

            //lets find all the project.json files in the src folder (don't care about versioning tests)
            var projectFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), Project.FileName, SearchOption.AllDirectories);

            //open them and convert them to source projects
            var projects = projectFiles.Select(x => ProjectReader.GetProject(x))
                            .Select(x => new SourceProject(x, repo.Info.WorkingDirectory))
                            .ToList();
            if (resetmode)
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
                    Console.WriteLine($"{p.Name} {p.Version.ToFullString()}");
                }
            }
            else
            { 
                // populate the dependency chains
                projects.ForEach(x => x.PopulateDependencies(projects));

           
                foreach (var p in projects)
                {
                    foreach (var c in repo.Commits)
                    {
                        // lets apply each commit to the projects looking to see if they effect it or a 
                        // dependency and detect if the commit was one that sent the current version
                        if (!p.ApplyCommit(c, repo))
                        {
                            // if it did create the version then stop looking this one wins
                            break;
                        }
                    }
                }

                // lets build version friendly commit
                string branch = repo.Head.FriendlyName;

                // lets see if we are running in appveyor
                var appveryorBranch = Environment.GetEnvironmentVariable("APPVEYOR_REPO_BRANCH");
                if (!string.IsNullOrWhiteSpace(appveryorBranch))
                {
                    branch = appveryorBranch;
                }

                var prNumber = Environment.GetEnvironmentVariable("APPVEYOR_PULL_REQUEST_NUMBER");
                if (!string.IsNullOrWhiteSpace(prNumber))
                {
                    branch = "PR" + prNumber;
                }
                if(branch == "(no branch)")
                {
                    throw new Exception("unable to find branch");
                }
                branch = branch.Replace("/", "-").Replace("--", "-");
                if (branch == "master")
                {
                    branch = "";
                }

                var sb = new StringBuilder();
                foreach (var p in projects)
                {
                    //TODO force update of all dependent projects to point to the newest build.

                    //we skip the build number and standard CI prefix on first commits
                    var newVersion = p.CalculateVersionNumber(branch);
                    File.Copy(p.FullProjectFilePath, $"{p.FullProjectFilePath}.bak", true);
                    dynamic projectFile = JsonConvert.DeserializeObject(File.ReadAllText(p.FullProjectFilePath));

                    projectFile.version = $"{newVersion}-*";
                    File.WriteAllText(p.FullProjectFilePath, JsonConvert.SerializeObject(projectFile, Formatting.Indented));
                    
                    Console.WriteLine($"{p.Name} {newVersion}");

                    sb.AppendLine($@"dotnet pack --configuration Release --output ""artifacts\bin\ImageSharp"" ""{p.ProjectFilePath}""");
                }

                File.Copy("build-inner.cmd", "build-inner.bak", true);
                File.WriteAllText("build-inner.cmd", sb.ToString());
            }
        }

        

        //find project root
        public static string GetProjectRoot()
        {
            var path = Path.GetFullPath(".");
            do
            {
                var jsonPath = Path.Combine(path, "global.json");
                if (File.Exists(jsonPath))
                {
                    return path;
                }
                else
                {
                    path = Path.GetDirectoryName(path);
                }
            } while (!string.IsNullOrWhiteSpace(path));

            return null;
        }

        public class SourceProject
        {
            private readonly IEnumerable<string> dependencies;

            public string ProjectDirectory { get; }

            public NuGetVersion Version { get; }

            public List<SourceProject> DependentProjects { get; private set; }
            public string Name { get; private set; }
            public string ProjectFilePath { get; private set; }

            private int commitCount = 0;
            public string FullProjectFilePath { get; private set; }

            public SourceProject(Project project, string root)
            {
                this.Name = project.Name;
                this.ProjectDirectory = project.ProjectDirectory.Substring(root.Length);
                this.ProjectFilePath = project.ProjectFilePath.Substring(root.Length);
                this.FullProjectFilePath = project.ProjectFilePath;
                this.Version = project.Version;
                this.dependencies = project.Dependencies.Select(x => x.Name);
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

            internal bool ApplyCommitInternal(Commit commit, TreeChanges changes, Repository repo)
            {
                commitCount++;

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
            internal bool ApplyCommit(Commit commit, Repository repo)
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

            internal string CalculateVersionNumber(string branch)
            {
                var version = this.Version.ToFullString();
                
                if (this.commitCount == 1 && branch == "") //master only
                {
                    if (this.Version.IsPrerelease)
                    {
                        //prerelease always needs the build counter just not on a branch name
                        return $"{version}-{this.commitCount:00000}";
                    }
                   
                    //only 1 commit (the changing one) we will skip appending suffix
                    return version;
                }
                

                var rootSpecialVersion = "";

                if (this.Version.IsPrerelease)
                {
                    var parts = version.Split(new[] { '-' }, 2);
                    version = parts[0];
                    rootSpecialVersion = parts[1];
                }
                if(rootSpecialVersion.Length > 0)
                {
                    rootSpecialVersion = "-" + rootSpecialVersion;
                }
                if (branch == "" && !this.Version.IsPrerelease)
                {
                    branch = fallbackTag;
                }
                if (branch.Length > 0)
                {
                    branch = "-" + branch;
                }

                var maxLength = 20;
                maxLength -= rootSpecialVersion.Length;
                maxLength -= 7; // for the counter and dashes
                if(branch.Length > maxLength)
                {
                    branch = branch.Substring(0, maxLength);
                }
                
                return $"{version}{rootSpecialVersion}{branch}-{this.commitCount:00000}";
            }
        }
    }
}
