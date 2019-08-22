// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using System;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    public interface IPipelinesExecutionContext: IExecutionContext
    {
        int BuildId { get; }

        long ContainerId { get; }

        string AccessToken { get; }
        
        string CollectionUri { get; }

        Guid ProjectId { get; }

        string TempPath { get; }
    }
}
