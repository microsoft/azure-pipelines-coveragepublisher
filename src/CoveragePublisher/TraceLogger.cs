// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public static class TraceLogger
    {
        private static ILogger _instance;
        
        public static void Initialize(ILogger logger)
        {
            _instance = logger;
        }

        /// <summary>
        /// Log error message.
        /// </summary>
        /// <param name="message">Message string.</param>
        public static void Error(string message)
        {
            if (_instance != null)
            {
                _instance.Error(message);
            }
        }

        /// <summary>
        /// Log verbose message.
        /// </summary>
        /// <param name="message">Message string.</param>
        public static void Verbose(string message)
        {
            if (_instance != null)
            {
                _instance.Verbose(message);
            }
        }

        /// <summary>
        /// Log warning message.
        /// </summary>
        /// <param name="message">Message string.</param>
        public static void Warning(string message)
        {
            if (_instance != null)
            {
                _instance.Warning(message);
            }
        }

        /// <summary>
        /// Log informational message.
        /// </summary>
        /// <param name="message">Message string.</param>
        public static void Info(string message)
        {
            if (_instance != null)
            {
                _instance.Info(message);
            }
        }

        /// <summary>
        /// Log debug message.
        /// </summary>
        /// <param name="message">Message string.</param>
        public static void Debug(string message)
        {
            if (_instance != null)
            {
                _instance.Debug(message);
            }
        }
    }
}
