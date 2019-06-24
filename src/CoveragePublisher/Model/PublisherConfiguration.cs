// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    /// <summary>
    /// Publisher configuration
    /// </summary>
    public class PublisherConfiguration
    {
        /// <summary>
        /// List of coverage files.
        /// </summary>
        virtual public IEnumerable<string> CoverageFiles { get; set; }

        /// <summary>
        /// Path to coverage report directory. If set to null or empty, publisher will not create/publish an html report
        /// </summary>
        virtual public string ReportDirectory { get; set; }

        /// <summary>
        /// Semi-colon separated list of source directories. Required for creating html reports for jacoco.
        /// </summary>
        virtual public string SourceDirectories { get; set; }
        
        /// <summary>
        /// Gets the configuration for whether HTML reports should be generated or not.
        /// </summary>
        virtual public bool GenerateHTMLReport {
            get
            {
                return !string.IsNullOrEmpty(ReportDirectory);
            }
        }

        /// <summary>
        /// Gets the timeout for coverage publisher.
        /// </summary>
        virtual public int TimeoutInSeconds { get; set; }

        /// <summary>
        /// Gets the configuration for whether telemetry is disabled.
        /// </summary>
        virtual public bool DisableTelemetry { get; set; }
    }
}
