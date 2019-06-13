using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    public class Parser
    {
        private PublisherConfiguration _configuration;
        private Lazy<ICoverageParserTool> _coverageParserTool;
        private IExecutionContext _context;

        public Parser(PublisherConfiguration config, IExecutionContext context)
        {
            _configuration = config;
            _coverageParserTool = new Lazy<ICoverageParserTool>(() => this.GetCoverageParserTool(_configuration));
            _context = context;
        }

        public List<FileCoverageInfo> GetFileCoverageInfos()
        {
            var tool = _coverageParserTool.Value;
            GenerateHTMLReport(tool);
            return tool.GetFileCoverageInfos();
        }

        public CoverageSummary GetCoverageSummary()
        {
            var tool = _coverageParserTool.Value;
            GenerateHTMLReport(tool);
            return tool.GetCoverageSummary();
        }
        
        protected void GenerateHTMLReport(ICoverageParserTool tool)
        {
            if (_configuration.GenerateHTMLReport)
            {
                try
                {
                    // Generate report
                    tool.GenerateHTMLReport();

                    // Copy coverage input files to report directory in a unique folder
                    if (Directory.Exists(_configuration.ReportDirectory))
                    {
                        string summaryFilesSubDir;

                        // Create a unique folder
                        do
                        {
                            summaryFilesSubDir = Path.Combine(_configuration.ReportDirectory, "Summary_" + Guid.NewGuid().ToString().Substring(0, 8));
                        } while (Directory.Exists(summaryFilesSubDir));

                        TraceLogger.Instance.Verbose("Parser.GenerateHTMLReport: Creating summary file directory: " + summaryFilesSubDir);

                        Directory.CreateDirectory(summaryFilesSubDir);

                        // Copy the files
                        foreach (var summaryFile in _configuration.CoverageFiles)
                        {
                            var summaryFileName = Path.GetFileName(summaryFile);
                            var destinationSummaryFile = Path.Combine(summaryFilesSubDir, summaryFileName);

                            TraceLogger.Instance.Verbose("Parser.GenerateHTMLReport: Copying summary file " + summaryFile);
                            File.Copy(summaryFile, destinationSummaryFile, true);
                        }
                    }
                    else
                    {
                        TraceLogger.Instance.Verbose("Parser.GenerateHTMLReport: Directory " + _configuration.ReportDirectory + " doesn't exist, skipping copying of coverage input files.");
                    }
                }
                catch (Exception e)
                {
                    TraceLogger.Instance.Error("Parser.GenerateHTMLReport: Error " + e);
                    _context.ConsoleLogger.Error(string.Format(Resources.HTMLReportError, e.Message));
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
