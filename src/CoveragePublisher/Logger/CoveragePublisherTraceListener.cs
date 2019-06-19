// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public class CoveragePublisherTraceListener : TextWriterTraceListener
    {
        public override void Write(string message)
        {
            base.Write(DateTime.Now.ToString("yyyy-MM-dd HH.mm.ssffff ") + message);
        }

        public override void WriteLine(string message)
        {
            Write(message + Environment.NewLine);
        }
    }
}