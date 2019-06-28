using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    public class BuildService
    {
        private readonly BuildHttpClient _buildHttpClient;
        private Guid _projectId;

        public BuildService(IClientFactory clientFactory, Guid projectId)
        {
            _projectId = projectId;
            _buildHttpClient = clientFactory.GetClient<BuildHttpClient>();
        }

        public virtual async Task<BuildArtifact> AssociateArtifact(
            int buildId,
            string name,
            string type,
            string data,
            Dictionary<string, string> propertiesDictionary,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            BuildArtifact artifact = new BuildArtifact()
            {
                Name = name,
                Resource = new ArtifactResource()
                {
                    Data = data,
                    Type = type,
                    Properties = propertiesDictionary
                }
            };

            return await _buildHttpClient.CreateArtifactAsync(artifact, _projectId, buildId, cancellationToken: cancellationToken);
        }

        public async Task<Build> UpdateBuildNumber(int buildId, string buildNumber, CancellationToken cancellationToken)
        {
            Build build = new Build()
            {
                Id = buildId,
                BuildNumber = buildNumber,
                Project = new TeamProjectReference()
                {
                    Id = _projectId,
                },
            };

            return await _buildHttpClient.UpdateBuildAsync(build, cancellationToken: cancellationToken);
        }

        public async Task<IEnumerable<string>> AddBuildTag(int buildId, string buildTag, CancellationToken cancellationToken)
        {
            return await _buildHttpClient.AddBuildTagAsync(_projectId, buildId, buildTag, cancellationToken: cancellationToken);
        }
    }
}
