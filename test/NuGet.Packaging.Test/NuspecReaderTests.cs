﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Text;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class NuspecReaderTests
    {
        private const string DuplicateGroups = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>packageA</id>
                        <version>1.0.1-alpha</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <dependencies>
                          <group targetFramework="".NETPortable0.0-net403+sl5+netcore45+wp8+MonoAndroid1+MonoTouch1"">
                            <dependency id=""Microsoft.Bcl.Async"" />
                            <dependency id=""Microsoft.Net.Http"" />
                            <dependency id=""Microsoft.Bcl.Build"" />
                          </group>
                          <group targetFramework="".NETPortable0.0-net403+sl5+netcore45+wp8"">
                            <dependency id=""Microsoft.Bcl.Async"" />
                            <dependency id=""Microsoft.Net.Http"" />
                            <dependency id=""Microsoft.Bcl.Build"" />
                          </group>
                        </dependencies>
                      </metadata>
                    </package>";

        private const string BasicNuspec = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                      <reference file=""a.dll"" />
                    </references>
                    <dependencies>
                        <group targetFramework=""net40"">
                          <dependency id=""jQuery"" />
                          <dependency id=""WebActivator"" version=""1.1.0"" />
                          <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                        </group>
                        <group targetFramework=""wp8"">
                          <dependency id=""jQuery"" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        private const string EmptyGroups = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                        <group>
                            <reference file=""a.dll"" />
                        </group>
                        <group targetFramework=""net45"" />
                    </references>
                    <dependencies>
                        <group targetFramework=""net40"">
                          <dependency id=""jQuery"" />
                          <dependency id=""WebActivator"" version=""1.1.0"" />
                          <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                        </group>
                        <group targetFramework=""net45"" />
                    </dependencies>
                  </metadata>
                </package>";

        // from: https://nuget.codeplex.com/wikipage?title=.nuspec%20v1.2%20Format
        private const string CommaDelimitedFrameworksNuspec = @"<?xml version=""1.0""?>
                <package>
                  <metadata>
                    <id>PackageWithGacReferences</id>
                    <version>1.0</version>
                    <authors>Author here</authors>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>A package that has framework assemblyReferences depending on the target framework.</description>
                    <frameworkAssemblies>
                      <frameworkAssembly assemblyName=""System.Web"" targetFramework=""net40"" />
                      <frameworkAssembly assemblyName=""System.Net"" targetFramework=""net40-client, net40"" />
                      <frameworkAssembly assemblyName=""Microsoft.Devices.Sensors"" targetFramework=""sl4-wp"" />
                      <frameworkAssembly assemblyName=""System.Json"" targetFramework=""sl3"" />
                      <frameworkAssembly assemblyName=""System.Windows.Controls.DomainServices"" targetFramework=""sl4"" />
                    </frameworkAssemblies>
                  </metadata>
                </package>";

        private const string NamespaceOnMetadataNuspec = @"<?xml version=""1.0""?>
                <package xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                    <metadata xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                    <id>packageB</id>
                    <version>1.0</version>
                    <authors>nuget</authors>
                    <owners>nuget</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>test</description>
                    </metadata>
                </package>";

        private const string UnknownDependencyGroupsNuspec = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                      <reference file=""a.dll"" />
                    </references>
                    <dependencies>
                        <group targetFramework=""net45"">
                          <dependency id=""jQuery"" />
                        </group>
                        <group targetFramework=""future51"">
                          <dependency id=""jQuery"" />
                        </group>
                        <group targetFramework=""future50"">
                          <dependency id=""jQuery"" />
                        </group>
                        <group targetFramework=""futurevnext10.0"">
                          <dependency id=""jQuery"" />
                        </group>
                        <group targetFramework=""some4~new5^conventions10"">
                          <dependency id=""jQuery"" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        [Fact]
        public void NuspecReaderTests_NamespaceOnMetadata()
        {
            NuspecReader reader = GetReader(NamespaceOnMetadataNuspec);

            string id = reader.GetId();

            Assert.Equal("packageB", id);
        }

        [Fact]
        public void NuspecReaderTests_Id()
        {
            NuspecReader reader = GetReader(BasicNuspec);

            string id = reader.GetId();

            Assert.Equal("packageA", id);
        }

        [Fact]
        public void NuspecReaderTests_EmptyGroups()
        {
            NuspecReader reader = GetReader(EmptyGroups);

            var dependencies = reader.GetDependencyGroups().ToList();
            var references = reader.GetReferenceGroups().ToList();

            Assert.Equal(2, dependencies.Count);
            Assert.Equal(2, references.Count);
        }

        [Fact]
        public void NuspecReaderTests_DependencyGroups()
        {
            NuspecReader reader = GetReader(BasicNuspec);

            var dependencies = reader.GetDependencyGroups().ToList();

            Assert.Equal(2, dependencies.Count);
        }

        [Fact]
        public void NuspecReaderTests_DuplicateDependencyGroups()
        {
            NuspecReader reader = GetReader(DuplicateGroups);

            var dependencies = reader.GetDependencyGroups().ToList();

            Assert.Equal(2, dependencies.Count);
        }

        [Fact]
        public void NuspecReaderTests_FrameworkGroups()
        {
            NuspecReader reader = GetReader(CommaDelimitedFrameworksNuspec);

            var dependencies = reader.GetFrameworkReferenceGroups().ToList();

            Assert.Equal(5, dependencies.Count);
        }

        [Fact]
        public void NuspecReaderTests_FrameworkSplitGroup()
        {
            NuspecReader reader = GetReader(CommaDelimitedFrameworksNuspec);

            var groups = reader.GetFrameworkReferenceGroups();

            var group = groups.Where(e => e.TargetFramework.Equals(NuGetFramework.Parse("net40"))).Single();

            Assert.Equal(2, group.Items.Count());

            Assert.Equal("System.Net", group.Items.ToArray()[0]);
            Assert.Equal("System.Web", group.Items.ToArray()[1]);
        }

        [Fact]
        public void NuspecReaderTests_Language()
        {
            NuspecReader reader = GetReader(BasicNuspec);

            var language = reader.GetLanguage();

            Assert.Equal("en-US", language);
        }

        [Fact]
        public void NuspecReaderTests_UnsupportedDependencyGroups()
        {
            NuspecReader reader = GetReader(UnknownDependencyGroupsNuspec);

            // verify we can handle multiple unsupported dependency groups gracefully
            var dependencies = reader.GetDependencyGroups().ToList();

            // unsupported frameworks remain ungrouped
            Assert.Equal(5, dependencies.Count);

            Assert.Equal(4, dependencies.Where(g => g.TargetFramework == NuGetFramework.UnsupportedFramework).Count());
        }

        private static NuspecReader GetReader(string nuspec)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(nuspec)))
            {
                return new NuspecReader(stream);
            }
        }
    }
}
