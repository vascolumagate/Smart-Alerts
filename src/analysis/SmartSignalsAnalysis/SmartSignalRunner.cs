﻿//-----------------------------------------------------------------------
// <copyright file="SmartSignalRunner.cs" company="Microsoft Corporation">
//        Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Monitoring.SmartSignals.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Monitoring.SmartSignals;
    using Microsoft.Azure.Monitoring.SmartSignals.Clients;
    using Microsoft.Azure.Monitoring.SmartSignals.Package;
    using Microsoft.Azure.Monitoring.SmartSignals.RuntimeShared;
    using Microsoft.Azure.Monitoring.SmartSignals.RuntimeShared.Exceptions;
    using Microsoft.Azure.Monitoring.SmartSignals.RuntimeShared.SignalResultPresentation;
    using Microsoft.Azure.Monitoring.SmartSignals.SignalLoader;
    using Microsoft.Azure.Monitoring.SmartSignals.SignalResultPresentation;
    using Microsoft.Azure.Monitoring.SmartSignals.Tools;

    /// <summary>
    /// An implementation of <see cref="ISmartSignalRunner"/>, that loads the signal and runs it
    /// </summary>
    public class SmartSignalRunner : ISmartSignalRunner
    {
        private readonly ISmartSignalRepository smartSignalRepository;
        private readonly ISmartSignalLoader smartSignalLoader;
        private readonly IAnalysisServicesFactory analysisServicesFactory;
        private readonly IAzureResourceManagerClient azureResourceManagerClient;
        private readonly IQueryRunInfoProvider queryRunInfoProvider;
        private readonly ITracer tracer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartSignalRunner"/> class
        /// </summary>
        /// <param name="smartSignalRepository">The smart signals repository</param>
        /// <param name="smartSignalLoader">The smart signals loader</param>
        /// <param name="analysisServicesFactory">The analysis services factory</param>
        /// <param name="azureResourceManagerClient">The azure resource manager client</param>
        /// <param name="queryRunInfoProvider">The query run information provider</param>
        /// <param name="tracer">The tracer</param>
        public SmartSignalRunner(
            ISmartSignalRepository smartSignalRepository,
            ISmartSignalLoader smartSignalLoader,
            IAnalysisServicesFactory analysisServicesFactory,
            IAzureResourceManagerClient azureResourceManagerClient,
            IQueryRunInfoProvider queryRunInfoProvider,
            ITracer tracer)
        {
            this.smartSignalRepository = Diagnostics.EnsureArgumentNotNull(() => smartSignalRepository);
            this.smartSignalLoader = Diagnostics.EnsureArgumentNotNull(() => smartSignalLoader);
            this.analysisServicesFactory = Diagnostics.EnsureArgumentNotNull(() => analysisServicesFactory);
            this.azureResourceManagerClient = Diagnostics.EnsureArgumentNotNull(() => azureResourceManagerClient);
            this.queryRunInfoProvider = Diagnostics.EnsureArgumentNotNull(() => queryRunInfoProvider);
            this.tracer = tracer;
        }

        #region Implementation of ISmartSignalRunner

        /// <summary>
        /// Loads the signal, runs it, and returns the generated result presentations
        /// </summary>
        /// <param name="request">The signal request</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A <see cref="Task{TResult}"/>, returning the list of Smart Signal result item presentations generated by the signal</returns>
        public async Task<List<SmartSignalResultItemPresentation>> RunAsync(SmartSignalRequest request, CancellationToken cancellationToken)
        {
            // Read the signal's package
            this.tracer.TraceInformation($"Loading signal package for signal ID {request.SignalId}");
            SmartSignalPackage signalPackage = await this.smartSignalRepository.ReadSignalPackageAsync(request.SignalId, cancellationToken);
            SmartSignalManifest signalManifest = signalPackage.Manifest;
            this.tracer.TraceInformation($"Read signal package, ID {signalManifest.Id}, Version {signalManifest.Version}");

            // Load the signal
            ISmartSignal signal = this.smartSignalLoader.LoadSignal(signalPackage);
            this.tracer.TraceInformation($"Signal instance loaded successfully, ID {signalManifest.Id}");

            // Get the resources on which to run the signal
            List<ResourceIdentifier> resources = await this.GetResourcesForSignal(request.ResourceIds, signalManifest, cancellationToken);

            // Run the signal
            this.tracer.TraceInformation($"Started running signal ID {signalManifest.Id}, Name {signalManifest.Name}");
            SmartSignalResult signalResult;
            try
            {
                var analysisRequest = new AnalysisRequest(resources, request.LastExecutionTime, request.Cadence, this.analysisServicesFactory);
                signalResult = await signal.AnalyzeResourcesAsync(analysisRequest, this.tracer, cancellationToken);
                this.tracer.TraceInformation($"Completed running signal ID {signalManifest.Id}, Name {signalManifest.Name}, returning {signalResult.ResultItems.Count} result items");
            }
            catch (Exception e)
            {
                this.tracer.TraceInformation($"Failed running signal ID {signalManifest.Id}, Name {signalManifest.Name}: {e.Message}");
                throw new SmartSignalCustomException(e.GetType().ToString(), e.Message, e.StackTrace);
            }

            // Verify that each result item belongs to one of the types declared in the signal manifest
            foreach (SmartSignalResultItem resultItem in signalResult.ResultItems)
            {
                if (!signalManifest.SupportedResourceTypes.Contains(resultItem.ResourceIdentifier.ResourceType))
                {
                    throw new UnidentifiedResultItemResourceTypeException(resultItem.ResourceIdentifier);
                }
            }

            // Trace the number of result items of each type
            foreach (var resultItemType in signalResult.ResultItems.GroupBy(x => x.GetType().Name))
            {
                this.tracer.TraceInformation($"Got {resultItemType.Count()} Smart Signal result items of type '{resultItemType.Key}'");
                this.tracer.ReportMetric("SignalResultItemType", resultItemType.Count(), new Dictionary<string, string>() { { "ResultItemType", resultItemType.Key } });
            }

            // Create results
            List<SmartSignalResultItemPresentation> results = new List<SmartSignalResultItemPresentation>();
            foreach (var resultItem in signalResult.ResultItems)
            {
                SmartSignalResultItemQueryRunInfo queryRunInfo = await this.queryRunInfoProvider.GetQueryRunInfoAsync(new List<ResourceIdentifier>() { resultItem.ResourceIdentifier }, cancellationToken);
                results.Add(SmartSignalResultItemPresentation.CreateFromResultItem(request, signalManifest.Name, resultItem, queryRunInfo));
            }

            this.tracer.TraceInformation($"Returning {results.Count} results");
            return results;
        }

        #endregion

        /// <summary>
        /// Verify that the request resource type is supported by the signal, and enumerate
        /// the resources that the signal should run on.
        /// </summary>
        /// <param name="requestResourceIds">The request resource Ids</param>
        /// <param name="smartSignalManifest">The signal manifest</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A <see cref="Task{TResult}"/>, returning the resource identifiers that the signal should run on</returns>
        private async Task<List<ResourceIdentifier>> GetResourcesForSignal(IList<string> requestResourceIds, SmartSignalManifest smartSignalManifest, CancellationToken cancellationToken)
        {
            HashSet<ResourceIdentifier> resourcesForSignal = new HashSet<ResourceIdentifier>();
            foreach (string requestResourceId in requestResourceIds)
            {
                ResourceIdentifier requestResource = ResourceIdentifier.CreateWithResourceId(requestResourceId);

                if (smartSignalManifest.SupportedResourceTypes.Contains(requestResource.ResourceType))
                {
                    // If the signal directly supports the requested resource type, then that's it
                    resourcesForSignal.Add(requestResource);
                }
                else if (requestResource.ResourceType == ResourceType.Subscription && smartSignalManifest.SupportedResourceTypes.Contains(ResourceType.ResourceGroup))
                {
                    // If the request is for a subscription, and the signal supports a resource group type, enumerate all resource groups in the requested subscription
                    IList<ResourceIdentifier> resourceGroups = await this.azureResourceManagerClient.GetAllResourceGroupsInSubscriptionAsync(requestResource.SubscriptionId, cancellationToken);
                    resourcesForSignal.UnionWith(resourceGroups);
                    this.tracer.TraceInformation($"Added {resourceGroups.Count} resource groups found in subscription {requestResource.SubscriptionId}");
                }
                else if (requestResource.ResourceType == ResourceType.Subscription)
                {
                    // If the request is for a subscription, enumerate all the resources in the requested subscription that the signal supports
                    IList<ResourceIdentifier> resources = await this.azureResourceManagerClient.GetAllResourcesInSubscriptionAsync(requestResource.SubscriptionId, smartSignalManifest.SupportedResourceTypes, cancellationToken);
                    resourcesForSignal.UnionWith(resources);
                    this.tracer.TraceInformation($"Added {resources.Count} resources found in subscription {requestResource.SubscriptionId}");
                }
                else if (requestResource.ResourceType == ResourceType.ResourceGroup && smartSignalManifest.SupportedResourceTypes.Any(type => type != ResourceType.Subscription))
                {
                    // If the request is for a resource group, and the signal supports resource types (other than subscription),
                    // enumerate all the resources in the requested resource group that the signal supports
                    IList<ResourceIdentifier> resources = await this.azureResourceManagerClient.GetAllResourcesInResourceGroupAsync(requestResource.SubscriptionId, requestResource.ResourceGroupName, smartSignalManifest.SupportedResourceTypes, cancellationToken);
                    resourcesForSignal.UnionWith(resources);
                    this.tracer.TraceInformation($"Added {resources.Count} resources found in the specified resource group in subscription {requestResource.SubscriptionId}");
                }
                else
                {
                    // The signal does not support the requested resource type
                    throw new IncompatibleResourceTypesException(requestResource.ResourceType, smartSignalManifest);
                }
            }

            return resourcesForSignal.ToList();
        }
    }
}