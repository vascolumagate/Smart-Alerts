﻿//-----------------------------------------------------------------------
// <copyright file="ISmartSignalRunner.cs" company="Microsoft Corporation">
//        Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Monitoring.SmartSignals.Analysis
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Monitoring.SmartSignals.RuntimeShared;
    using Microsoft.Azure.Monitoring.SmartSignals.SignalResultPresentation;

    /// <summary>
    /// An interface for running a smart signal
    /// </summary>
    public interface ISmartSignalRunner
    {
        /// <summary>
        /// Runs the signal
        /// </summary>
        /// <param name="request">The signal request</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A <see cref="Task{TResult}"/>, returning the list of Smart Signal result item presentations generated by the signal</returns>
        Task<List<SmartSignalResultItemPresentation>> RunAsync(SmartSignalRequest request, CancellationToken cancellationToken);
    }
}