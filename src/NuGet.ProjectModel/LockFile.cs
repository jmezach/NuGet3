// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFile
    {
        public bool IsLocked { get; set; }
        public int Version { get; set; }
        public IList<ProjectFileDependencyGroup> ProjectFileDependencyGroups { get; set; } = new List<ProjectFileDependencyGroup>();
        public IList<LockFileLibrary> Libraries { get; set; } = new List<LockFileLibrary>();
        public IList<LockFileTarget> Targets { get; set; } = new List<LockFileTarget>();

        public bool IsValidForPackageSpec(PackageSpec spec)
        {
            if (Version != LockFileFormat.Version)
            {
                return false;
            }

            var actualTargetFrameworks = spec.TargetFrameworks;

            // The lock file should contain dependencies for each framework plus dependencies shared by all frameworks
            if (ProjectFileDependencyGroups.Count != actualTargetFrameworks.Count() + 1)
            {
                return false;
            }

            foreach (var group in ProjectFileDependencyGroups)
            {
                IOrderedEnumerable<string> actualDependencies;
                var expectedDependencies = group.Dependencies.OrderBy(x => x);

                // If the framework name is empty, the associated dependencies are shared by all frameworks
                if (string.IsNullOrEmpty(group.FrameworkName))
                {
                    actualDependencies = spec.Dependencies.Select(x => RuntimeStyleLibraryRangeToString(x.LibraryRange)).OrderBy(x => x);
                }
                else
                {
                    var framework = actualTargetFrameworks
                        .FirstOrDefault(f =>
                            string.Equals(f.FrameworkName.ToString(), group.FrameworkName, StringComparison.OrdinalIgnoreCase));
                    if (framework == null)
                    {
                        return false;
                    }

                    actualDependencies = framework.Dependencies.Select(d => RuntimeStyleLibraryRangeToString(d.LibraryRange)).OrderBy(x => x);
                }

                if (!actualDependencies.SequenceEqual(expectedDependencies))
                {
                    return false;
                }
            }

            return true;
        }

        // DNU REFACTORING TODO: temp hack to make generated lockfile work with runtime lockfile validation
        internal static string RuntimeStyleLibraryRangeToString(LibraryRange libraryRange)
        {
            return RuntimeStyleLibraryRangeToString(libraryRange.Name, libraryRange.VersionRange);
        }

        internal static string RuntimeStyleLibraryRangeToString(string name, VersionRange versionRange)
        {
            var sb = new StringBuilder();
            sb.Append(name);
            sb.Append(" ");

            if (versionRange == null)
            {
                return sb.ToString();
            }

            var minVersion = versionRange.MinVersion;
            var maxVersion = versionRange.MaxVersion;

            sb.Append(">= ");

            if (versionRange.IsFloating)
            {
                sb.Append(versionRange.Float.ToString());
            }
            else
            {
                sb.Append(minVersion.ToString());
            }

            if (maxVersion != null)
            {
                sb.Append(versionRange.IsMaxInclusive ? "<= " : "< ");
                sb.Append(maxVersion.Version.ToString());
            }

            return sb.ToString();
        }

        public LockFileTarget GetTarget(NuGetFramework framework, string runtimeIdentifier)
        {
            return Targets.FirstOrDefault(t =>
                t.TargetFramework.Equals(framework) &&
                ((string.IsNullOrEmpty(runtimeIdentifier) && string.IsNullOrEmpty(t.RuntimeIdentifier) ||
                 string.Equals(runtimeIdentifier, t.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase))));
        }

        public LockFileLibrary GetLibrary(string name, NuGetVersion version)
        {
            return Libraries.FirstOrDefault(l =>
                string.Equals(l.Name, name) &&
                l.Version.Equals(version));
        }
    }
}
