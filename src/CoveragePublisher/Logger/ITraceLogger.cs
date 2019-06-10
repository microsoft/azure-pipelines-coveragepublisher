// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public interface ITraceLogger
    {
        /// <summary>
        /// Verbose diagnostics.
        /// </summary>
        /// <param name="text">Diagnostics text.</param>
        void Verbose(string message, params object[] args);

        /// <summary>
        /// Info diagnostics.
        /// </summary>
        /// <param name="text">Diagnostics text.</param>
        void Info(string message, params object[] args);

        /// <summary>
        /// Warning diagnostics.
        /// </summary>
        /// <param name="text">Diagnostics text.</param>
        void Warning(string message, params object[] args);

        /// <summary>
        /// Error diagnostics.
        /// </summary>
        /// <param name="text">Diagnostics text.</param>
        void Error(string message, params object[] args);
    }
}
