// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    /// <summary>
    /// Interface for parsed command line arguments.
    /// </summary>
    interface ICLIArgs
    {
        /// <summary>
        /// List of coverage files.
        /// </summary>
        IEnumerable<string> CoverageFiles { get; set; }

        /// <summary>
        /// Path to coverage report directory.
        /// </summary>
        string ReportDirectory { get; set; }
    }
}
