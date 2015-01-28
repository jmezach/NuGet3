﻿using Newtonsoft.Json.Linq;
using NuGet.Client;
using NuGet.Client.V3;
using NuGet.Client.VisualStudio;
using NuGet.Data;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace NuGet.Client.V3.VisualStudio
{
    public class V3UISearchResource : UISearchResource
    {
        private readonly V3RawSearchResource _searchResource;
        private readonly UIMetadataResource _metadataResource;

        public V3UISearchResource(V3RawSearchResource searchResource, UIMetadataResource metadataResource)
            : base()
        {
            _searchResource = searchResource;
            _metadataResource = metadataResource;
        }

        public override async Task<IEnumerable<UISearchMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            List<UISearchMetadata> visualStudioUISearchResults = new List<UISearchMetadata>();

            var searchResultJsonObjects = await _searchResource.Search(searchTerm, filters, skip, take, cancellationToken);

            foreach (JObject searchResultJson in searchResultJsonObjects)
            {
                 visualStudioUISearchResults.Add(await GetVisualStudioUISearchResult(searchResultJson, filters.IncludePrerelease, cancellationToken));
            }

            return visualStudioUISearchResults;
        }

        private async Task<UISearchMetadata> GetVisualStudioUISearchResult(JObject package, bool includePrerelease, CancellationToken token)
        {
            string id = package.Value<string>(Properties.PackageId);
            NuGetVersion version = NuGetVersion.Parse(package.Value<string>(Properties.Version));

            PackageIdentity topPackage = new PackageIdentity(id, version);

            Uri iconUrl = GetUri(package, Properties.IconUrl);

            // get other versions
            var versionList = new List<NuGetVersion>();
            var versions = package.Value<JArray>(Properties.Versions);
            if (versions != null)
            {
                if (versions[0].Type == JTokenType.String)
                {
                    // TODO: this part should be removed once the new end point is up and running.
                    versionList = versions
                        .Select(v => NuGetVersion.Parse(v.Value<string>()))
                        .ToList();
                }
                else
                {
                    versionList = versions
                        .Select(v => NuGetVersion.Parse(v.Value<string>("version")))
                        .ToList();
                }

                if (!includePrerelease)
                {
                    // remove prerelease version if includePrelease is false
                    versionList.RemoveAll(v => v.IsPrerelease);
                }
            }
            if (!versionList.Contains(version))
            {
                versionList.Add(version);
            }

            IEnumerable<NuGetVersion> nuGetVersions = versionList;
            string summary = package.Value<string>(Properties.Summary);
            if (string.IsNullOrWhiteSpace(summary))
            {
                // summary is empty. Use its description instead.
                summary = package.Value<string>(Properties.Description);
            }

            // retrieve metadata for the top package
            UIPackageMetadata metadata = null;

            V3UIMetadataResource v3metadataRes = _metadataResource as V3UIMetadataResource;

            // for v3 just parse the data from the search results
            if (v3metadataRes != null)
            {
                metadata = v3metadataRes.ParseMetadata(package);
            }

            // if we do not have a v3 metadata resource, request it using whatever is available
            if (metadata == null)
            {
                metadata = await _metadataResource.GetMetadata(topPackage, token);
            }

            UISearchMetadata searchResult = new UISearchMetadata(topPackage, summary, iconUrl, nuGetVersions, metadata);
            return searchResult;
        }

        /// <summary>
        /// Returns a field value or the empty string. Arrays will become comma delimited strings.
        /// </summary>
        private static string GetField(JObject json, string property)
        {
            JToken value = json[property];

            if (value == null)
            {
                return string.Empty;
            }

            JArray array = value as JArray;

            if (array != null)
            {
                return String.Join(", ", array.Select(e => e.ToString()));
            }

            return value.ToString();
        }

        private static int GetInt(JObject json, string property)
        {
            JToken value = json[property];

            if (value == null)
            {
                return 0;
            }

            return value.ToObject<int>();
        }

        private static DateTimeOffset? GetDateTime(JObject json, string property)
        {
            JToken value = json[property];

            if (value == null)
            {
                return null;
            }

            return value.ToObject<DateTimeOffset>();
        }


        private Uri GetUri(JObject json, string property)
        {
            if (json[property] == null)
            {
                return null;
            }
            string str = json[property].ToString();
            if (String.IsNullOrEmpty(str))
            {
                return null;
            }
            return new Uri(str);
        }
    }
}
