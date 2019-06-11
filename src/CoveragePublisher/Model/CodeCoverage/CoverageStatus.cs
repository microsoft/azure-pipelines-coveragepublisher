// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    /// <summary>
    /// Line coverage status.
    /// </summary>
    public enum CoverageStatus
    {
        Covered,
        NotCovered,
        PartiallyCovered
    }
}
