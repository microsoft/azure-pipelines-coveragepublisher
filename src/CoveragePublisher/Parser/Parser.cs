using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    public class Parser
    {
        public List<FileCoverageInfo> GetFileCoverageInfos(PublisherConfiguration config)
        {
            var tool = GetCoverageParserTool(config);
            GenerateHTMLReport(tool, config);
            return tool.GetFileCoverageInfos();
        }

        public CoverageSummary GetCoverageSummary(PublisherConfiguration config)
        {
            var tool = GetCoverageParserTool(config);
            GenerateHTMLReport(tool, config);
            return tool.GetCoverageSummary();
        }
        
        protected void GenerateHTMLReport(ICoverageParserTool tool, PublisherConfiguration config)
        {
            if (config.GenerateHTMLReport)
            {
                try
                {
                    // Generate report
                    tool.GenerateHTMLReport();

                    // Copy coverage input files to report directory in a unique folder
                    if (Directory.Exists(config.ReportDirectory))
                    {

                        string summaryFilesSubDir;

                        // Create a unique folder
                        do
                        {
                            summaryFilesSubDir = Path.Combine(config.ReportDirectory, "Summary_" + Guid.NewGuid().ToString().Substring(0, 8));
                        } while (Directory.Exists(summaryFilesSubDir));

                        TraceLogger.Instance.Verbose("Parser.GenerateHTMLReport: Creating summary file directory: " + summaryFilesSubDir);

                        Directory.CreateDirectory(summaryFilesSubDir);

                        // Copy the files
                        foreach (var summaryFile in config.CoverageFiles)
                        {
                            var summaryFileName = Path.GetFileName(summaryFile);
                            var destinationSummaryFile = Path.Combine(summaryFilesSubDir, summaryFileName);

                            TraceLogger.Instance.Verbose("Parser.GenerateHTMLReport: Copying summary file " + summaryFile);
                            File.Copy(summaryFile, destinationSummaryFile, true);
                        }
                    }
                    else
                    {
                        TraceLogger.Instance.Verbose("Parser.GenerateHTMLReport: Directory " + config.ReportDirectory + " doesn't exist, skipping copying of coverage input files.");
                    }
                }
                catch (Exception e)
                {
                    // Throw?
                    TraceLogger.Instance.Error("Parser.GenerateHTMLReport: Error " + e);
                }
            }
        }

        private ICoverageParserTool GetCoverageParserTool(PublisherConfiguration config)
        {
            // Currently there's only one parser tool, so simply return that instead of having a factory
            return new ReportGeneratorTool(config);
        }
    }
}
