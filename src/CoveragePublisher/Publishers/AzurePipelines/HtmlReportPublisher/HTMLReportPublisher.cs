using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal class HTMLReportPublisher
    {
        private PipelinesExecutionContext _executionContext;
        private ClientFactory _clientFactory;

        public HTMLReportPublisher(PipelinesExecutionContext executionContext, ClientFactory clientFactory)
        {
            _executionContext = executionContext;
            _clientFactory = clientFactory;
        }

        public async Task PublishHTMLReportAsync(PipelinesExecutionContext executionContext, string reportDirectory, CancellationToken cancellationToken)
        {
            var buildId = executionContext.BuildId;

            string destinationSummaryFile = null;
            var newReportDirectory = reportDirectory;
            try
            {
                var filesToPublish = new List<Tuple<string, string>>();

                if (!Directory.Exists(newReportDirectory))
                {
                    if (!string.IsNullOrWhiteSpace(newReportDirectory))
                    {
                        // Report directory was invalid. Write error and continue.
                        executionContext.ConsoleLogger.Error(string.Format(Resources.DirectoryNotFound, newReportDirectory));
                    }

                    // Use a new temp directory
                    newReportDirectory = Path.Combine(Path.GetTempPath(), GetCoverageDirectoryName(buildId, Constants.ReportDirectory));
                    Directory.CreateDirectory(newReportDirectory);
                }
                
                executionContext.ConsoleLogger.Info(Resources.ModifyingCoberturaIndexFile);
                ModifyCoberturaIndexDotHTML(newReportDirectory, executionContext);

                filesToPublish.Add(new Tuple<string, string>(newReportDirectory, GetCoverageDirectoryName(buildId, Constants.ReportDirectory)));

                ChangeHtmExtensionToHtmlIfRequired(newReportDirectory, executionContext);

                executionContext.ConsoleLogger.Info(Resources.PublishingCodeCoverageFiles);

                await this.PublishCodeCoverageFilesAsync(executionContext, filesToPublish, File.Exists(Path.Combine(newReportDirectory, Constants.DefaultIndexFile)), cancellationToken);
            }
            catch (Exception ex)
            {
                executionContext.ConsoleLogger.Warning(string.Format(Resources.ErrorOccurredWhilePublishingCCFiles, ex.Message));
            }
            finally
            {
                if (!string.IsNullOrEmpty(destinationSummaryFile))
                {
                    var summaryFileDirectory = Path.GetDirectoryName(destinationSummaryFile);
                    if (Directory.Exists(summaryFileDirectory))
                    {
                        Directory.Delete(path: summaryFileDirectory, recursive: true);
                    }
                }

                if (!Directory.Exists(reportDirectory))
                {
                    if (Directory.Exists(newReportDirectory))
                    {
                        //delete the generated report directory
                        Directory.Delete(path: newReportDirectory, recursive: true);
                    }
                }
            }
        }

        private async Task PublishCodeCoverageFilesAsync(PipelinesExecutionContext executionContext, List<Tuple<string, string>> files, bool browsable, CancellationToken cancellationToken)
        {
            var publishCCTasks = files.Select(async file =>
            {
                var browsableProperty = (browsable) ? bool.TrueString : bool.FalseString;
                var artifactProperties = new Dictionary<string, string> {
                    { Constants.ArtifactUploadEventProperties.ContainerFolder, file.Item2},
                    { Constants.ArtifactUploadEventProperties.ArtifactName, file.Item2 },
                    { Constants.ArtifactUploadEventProperties.ArtifactType, ArtifactResourceTypes.Container },
                    { Constants.ArtifactUploadEventProperties.Browsable, browsableProperty },
                };

                // Upload report directory to file container service.
                var fileService = new FileContainerService(_clientFactory, executionContext.ProjectId, executionContext.ContainerId, file.Item2);
                await fileService.CopyToContainerAsync(executionContext.ConsoleLogger, file.Item1, cancellationToken);
                string fileContainerFullPath = string.Format($"#/{executionContext.ContainerId}/{file.Item2}");

                // Associate the uploaded directory with build artifact.
                var buildService = new BuildService(_clientFactory, executionContext.ProjectId);
                await buildService.AssociateArtifact(executionContext.BuildId, file.Item2, ArtifactResourceTypes.Container, fileContainerFullPath, artifactProperties, cancellationToken);
                executionContext.ConsoleLogger.Info(string.Format(Resources.PublishedCodeCoverageArtifact, file.Item1, file.Item2));
            });

            await Task.WhenAll(publishCCTasks);
        }

        /// <summary>
        /// This method replaces the default index.html generated by cobertura with
        /// the non-framed version
        /// </summary>
        /// <param name="reportDirectory"></param>
        private void ModifyCoberturaIndexDotHTML(string reportDirectory, PipelinesExecutionContext executionContext)
        {
            string newIndexHtml = Path.Combine(reportDirectory, Constants.NewIndexFile);
            string indexHtml = Path.Combine(reportDirectory, Constants.DefaultIndexFile);
            string nonFrameHtml = Path.Combine(reportDirectory, Constants.DefaultNonFrameFileCobertura);

            try
            {
                if (File.Exists(indexHtml) && File.Exists(nonFrameHtml))
                {
                    // duplicating frame-summary.html to index.html and renaming index.html to newindex.html
                    File.Delete(newIndexHtml);
                    File.Move(indexHtml, newIndexHtml);
                    File.Copy(nonFrameHtml, indexHtml, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                // In the warning text, prefer using ex.InnerException when available, for more-specific details
                executionContext.ConsoleLogger.Warning(string.Format(Resources.RenameIndexFileCoberturaFailed, indexHtml, newIndexHtml, (ex.InnerException ?? ex).ToString()));
            }
        }

        private string GetCoverageDirectoryName(int buildId, string directoryName)
        {
            return directoryName + "_" + buildId.ToString();
        }

        // Changes the index.htm file to index.html if index.htm exists
        private void ChangeHtmExtensionToHtmlIfRequired(string reportDirectory, PipelinesExecutionContext executionContext)
        {
            var defaultIndexFile = Path.Combine(reportDirectory, Constants.DefaultIndexFile);
            var htmIndexFile = Path.Combine(reportDirectory, Constants.HtmIndexFile);

            // If index.html does not exist and index.htm exists, copy the .html file from .htm file.
            // Don't delete the .htm file as it might be referenced by other .htm/.html files.
            if (!File.Exists(defaultIndexFile) && File.Exists(htmIndexFile))
            {
                try
                {
                    File.Copy(htmIndexFile, defaultIndexFile);
                }
                catch (Exception ex)
                {
                    // In the warning text, prefer using ex.InnerException when available, for more-specific details
                    executionContext.ConsoleLogger.Warning(string.Format(Resources.RenameIndexFileCoberturaFailed, htmIndexFile, defaultIndexFile, (ex.InnerException ?? ex).ToString()));
                }
            }
        }


    }
}
