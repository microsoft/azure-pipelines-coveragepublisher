using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    internal class HtmlReportPublisher: IHtmlReportPublisher
    {
        private IPipelinesExecutionContext _executionContext;
        private IClientFactory _clientFactory;
        private ServiceFactory _serviceFactory;

        public HtmlReportPublisher(
            IPipelinesExecutionContext executionContext,
            IClientFactory clientFactory,
            ServiceFactory serviceFactory)
        {
            _executionContext = executionContext;
            _clientFactory = clientFactory;
            _serviceFactory = serviceFactory;
        }

        public HtmlReportPublisher(IPipelinesExecutionContext executionContext, IClientFactory clientFactory):
            this(executionContext, clientFactory, new ServiceFactory()) { }

        public async Task PublishHTMLReportAsync(string reportDirectory, CancellationToken cancellationToken)
        {
            var buildId = _executionContext.BuildId.ToString();

            try
            {
                // map upload directory to its container name
                var uploadDirectories = new List<Tuple<string, string>>();

                if (!Directory.Exists(reportDirectory))
                {
                    throw new DirectoryNotFoundException(string.Format(Resources.DirectoryNotFound, reportDirectory));
                }

                RenameIndexHtmExtensionIfRequired(reportDirectory);
                uploadDirectories.Add(new Tuple<string, string>(reportDirectory, GetCoverageDirectoryName(buildId)));

                TraceLogger.Info(Resources.PublishingCodeCoverageReport);

                await this.PublishCodeCoverageFilesAsync(uploadDirectories, File.Exists(Path.Combine(reportDirectory, Constants.DefaultIndexFile)), cancellationToken);
            }
            catch (Exception ex)
            {
                TraceLogger.Error(string.Format(Resources.ErrorOccurredWhilePublishingCCFiles, ex));
            }
        }

        private async Task PublishCodeCoverageFilesAsync(List<Tuple<string, string>> directoriesAndcontainerPaths, bool browsable, CancellationToken cancellationToken)
        {
            var publishCCTasks = directoriesAndcontainerPaths.Select(async tuple =>
            {
                var browsableProperty = (browsable) ? bool.TrueString : bool.FalseString;
                var artifactProperties = new Dictionary<string, string> {
                    { Constants.ArtifactUploadEventProperties.ContainerFolder, tuple.Item2},
                    { Constants.ArtifactUploadEventProperties.ArtifactName, tuple.Item2 },
                    { Constants.ArtifactUploadEventProperties.ArtifactType, ArtifactResourceTypes.Container },
                    { Constants.ArtifactUploadEventProperties.Browsable, browsableProperty },
                };

                // Upload to file container service
                var fileContainerHelper = _serviceFactory.GetFileContainerService(_clientFactory, _executionContext);
                await fileContainerHelper.CopyToContainerAsync(tuple, cancellationToken);
                string fileContainerFullPath = string.Format($"#/{_executionContext.ContainerId}/{tuple.Item2}");

                // Associate with build artifact
                var buildHelper = _serviceFactory.GetBuildService(_clientFactory, _executionContext);
                await buildHelper.AssociateArtifact(_executionContext.BuildId, tuple.Item2, ArtifactResourceTypes.Container, fileContainerFullPath, artifactProperties, cancellationToken);
                TraceLogger.Info(string.Format(Resources.PublishedCodeCoverageArtifact, tuple.Item1, tuple.Item2));
            });

            await Task.WhenAll(publishCCTasks);

            // TODO task exceptions? 
        }

        private string GetCoverageDirectoryName(string buildId)
        {
            return Constants.ReportDirectory + "_" + buildId;
        }

        private void RenameIndexHtmExtensionIfRequired(string reportDirectory)
        {
            try
            {
                var indexHtm = Path.Combine(reportDirectory, Constants.HtmIndexFile);
                var indexHtml = Path.Combine(reportDirectory, Constants.DefaultIndexFile);

                if (File.Exists(indexHtm) && !File.Exists(indexHtml))
                {
                    File.Move(indexHtm, indexHtml);
                }
            }
            catch(Exception ex)
            {
                TraceLogger.Error(string.Format(Resources.CouldNotRenameExtension, ex));
            }
        }
    }
}
