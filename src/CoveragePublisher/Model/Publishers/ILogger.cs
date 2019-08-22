// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    /// <summary>
    /// Different from ITraceLogger, this class is for logging to execution context.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Log error message.
        /// </summary>
        /// <param name="message">Message string.</param>
        void Error(string message);

        /// <summary>
        /// Log verbose message.
        /// </summary>
        /// <param name="message">Message string.</param>
        void Verbose(string message);

        /// <summary>
        /// Log warning message.
        /// </summary>
        /// <param name="message">Message string.</param>
        void Warning(string message);

        /// <summary>
        /// Log informational message.
        /// </summary>
        /// <param name="message">Message string.</param>
        void Info(string message);

        /// <summary>
        /// Log debug message.
        /// </summary>
        /// <param name="message">Message string.</param>
        void Debug(string message);
    }
}
