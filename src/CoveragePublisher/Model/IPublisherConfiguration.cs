// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    /// <summary>
    /// Interface for parsed command line arguments.
    /// </summary>
    interface IPublisherConfiguration
    {
        /// <summary>
        /// List of coverage files.
        /// </summary>
        IEnumerable<string> CoverageFiles { get; set; }

        /// <summary>
        /// Path to coverage report directory. If set to null or empty, publisher will not create/publish an html report
        /// </summary>
        string ReportDirectory { get; set; }

        /// <summary>
        /// Path to directory containing the source. Required for creating html reports for jacoco
        /// </summary>
        string SourceDirectory { get; set; }
    }
}
