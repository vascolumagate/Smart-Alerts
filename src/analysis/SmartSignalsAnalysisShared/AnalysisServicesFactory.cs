﻿//-----------------------------------------------------------------------
// <copyright file="AnalysisServicesFactory.cs" company="Microsoft Corporation">
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
    using Microsoft.Azure.Monitoring.SmartSignals.Shared;
    using Microsoft.Azure.Monitoring.SmartSignals.Shared.HttpClient;

    /// <summary>
    /// An implementation of the <see cref="IAnalysisServicesFactory"/> interface.
    /// </summary>
    public class AnalysisServicesFactory : IAnalysisServicesFactory
    {
        private const int MaxNumberOfResourcesInQuery = 10;

        private readonly IHttpClientWrapper httpClientWrapper;
        private readonly IAzureResourceManagerClient azureResourceManagerClient;
        private readonly TimeSpan queryTimeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnalysisServicesFactory"/> class.
        /// </summary>
        /// <param name="httpClientWrapper">The HTTP client wrapper.</param>
        /// <param name="azureResourceManagerClient">The azure resource manager client.</param>
        public AnalysisServicesFactory(IHttpClientWrapper httpClientWrapper, IAzureResourceManagerClient azureResourceManagerClient)
        {
            this.httpClientWrapper = httpClientWrapper;
            this.azureResourceManagerClient = azureResourceManagerClient;

            string timeoutString = ConfigurationReader.ReadConfig("AnalyticsQueryTimeoutInMinutes", required: true);
            this.queryTimeout = TimeSpan.FromMinutes(int.Parse(timeoutString));
        }

        /// <summary>
        /// Creates an instance of <see cref="IQueryClient"/>, used for running queries against data in log analytics workspaces.
        /// </summary>
        /// <param name="resources">The list of resources to analyze.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <exception cref="QueryClientCreationException">A log analytics query client could not be created for the specified resources.</exception>
        /// <returns>The query client, that can be used to run queries on log analytics workspaces.</returns>
        public async Task<IQueryClient> CreateLogAnalyticsQueryClientAsync(IReadOnlyList<ResourceIdentifier> resources, CancellationToken cancellationToken)
        {
            // Basic validation
            this.VerifyResources(resources);

            // Verify that there are no application insights resources
            if (resources.Any(resource => resource.ResourceType == ResourceType.ApplicationInsights))
            {
                throw new QueryClientCreationException("A log analytics client cannot be created to access application insights resources");
            }

            IReadOnlyList<ResourceIdentifier> workspaces;
            if (resources.All(resource => resource.ResourceType == ResourceType.LogAnalytics))
            {
                // All resources are of type LogAnalytics. Create a client that queries all these workspaces.
                workspaces = resources;
            }
            else
            {
                // Since we do not know where the telemetry of each resource is, create a client that queries all workspaces in the subscription.
                var workspacesList = new List<ResourceIdentifier>();
                foreach (string subscriptionId in resources.Select(resource => resource.SubscriptionId).Distinct(StringComparer.CurrentCultureIgnoreCase))
                {
                    workspacesList.AddRange(await this.azureResourceManagerClient.GetAllResourcesInSubscriptionAsync(subscriptionId, new[] { ResourceType.LogAnalytics }, cancellationToken));
                }

                workspaces = workspacesList;
                if (workspaces.Count == 0)
                {
                    throw new InvalidOperationException("No log analytics workspaces were found");
                }
            }

            // Verify there are not too many resources
            if (workspaces.Count > MaxNumberOfResourcesInQuery)
            {
                throw new QueryClientCreationException($"Cannot run analysis on more than {MaxNumberOfResourcesInQuery} workspaces");
            }

            // Get workspace Id (for the 1st workspace)
            string workspaceId = await this.azureResourceManagerClient.GetLogAnalyticsWorkspaceIdAsync(workspaces[0], cancellationToken);

            // Create the client
            return new LogAnalyticsQueryClient(this.httpClientWrapper, workspaceId, workspaces.Select(workspace => this.azureResourceManagerClient.GetResourceId(workspace)), this.queryTimeout);
        }

        /// <summary>
        /// Creates an instance of <see cref="IQueryClient"/>, used for running queries against data in application insights.
        /// </summary>
        /// <param name="resources">The list of resources to analyze.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <exception cref="QueryClientCreationException">An application insights query client could not be created for the specified resources.</exception>
        /// <returns>The query client, that can be used to run queries on application insights.</returns>
        public async Task<IQueryClient> CreateApplicationInsightsQueryClientAsync(IReadOnlyList<ResourceIdentifier> resources, CancellationToken cancellationToken)
        {
            // Basic validation
            this.VerifyResources(resources);

            // Verify that all resources are of type ApplicationInsights
            if (resources.Any(resource => resource.ResourceType != ResourceType.ApplicationInsights))
            {
                throw new QueryClientCreationException("An application insights query client can only be created for resources of type ApplicationInsights");
            }

            // Verify there are not too many resources
            if (resources.Count > MaxNumberOfResourcesInQuery)
            {
                throw new QueryClientCreationException($"Cannot run analysis on more than {MaxNumberOfResourcesInQuery} applications");
            }

            // Get application Id (for the 1st application)
            string applicationId = await this.azureResourceManagerClient.GetApplicationInsightsAppIdAsync(resources[0], cancellationToken);

            // Create the client
            return new ApplicationInsightsQueryClient(this.httpClientWrapper, applicationId, resources.Select(resource => this.azureResourceManagerClient.GetResourceId(resource)), this.queryTimeout);
        }

        /// <summary>
        /// Perform basic validations on the resources to analyze.
        /// </summary>
        /// <param name="resources">The resources to analyze</param>
        private void VerifyResources(IReadOnlyList<ResourceIdentifier> resources)
        {
            // Verify that there are resources
            if (!resources.Any())
            {
                throw new QueryClientCreationException("No resources provided");
            }
        }
    }
}