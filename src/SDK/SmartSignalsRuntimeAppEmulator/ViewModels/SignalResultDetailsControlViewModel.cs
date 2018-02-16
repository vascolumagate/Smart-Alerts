﻿//-----------------------------------------------------------------------
// <copyright file="SignalResultDetailsControlViewModel.cs" company="Microsoft Corporation">
//        Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Monitoring.SmartSignals.Emulator.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Monitoring.SmartSignals.Emulator.Models;
    using Microsoft.Azure.Monitoring.SmartSignals.SignalResultPresentation;
    using Unity.Attributes;

    /// <summary>
    /// The view model class for the <see cref="SignalResultDetailsControlViewModel"/> control.
    /// </summary>
    public class SignalResultDetailsControlViewModel : ObservableObject
    {
        private SignalResultItem signalResult;

        private ObservableCollection<AzureResourceProperty> essentialsSectionProperties;

        private ObservableCollection<SmartSignalResultItemPresentationProperty> propertiesSectionProperties;

        private ObservableCollection<SmartSignalResultItemPresentationProperty> analysisSectionProperties;

        private ObservableCollection<AnalyticsQuery> analyticsQueries;

        #region Ctros

        /// <summary>
        /// Initializes a new instance of the <see cref="SignalResultDetailsControlViewModel"/> class for design time only.
        /// </summary>
        public SignalResultDetailsControlViewModel()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SignalResultDetailsControlViewModel" /> class.
        /// </summary>
        /// <param name="signalResult">The smart signal runner.</param>
        /// <param name="resultDetailsControlClosed">The smart signal runner gunner.</param>
        public SignalResultDetailsControlViewModel(SignalResultItem signalResult, ResultDetailsControlClosedEventHandler resultDetailsControlClosed)
        {
            this.SignalResult = signalResult;

            this.EssentialsSectionProperties = new ObservableCollection<AzureResourceProperty>(new List<AzureResourceProperty>()
                {
                    new AzureResourceProperty("Subscription name", this.SignalResult.ResourceIdentifier.ResourceName),
                    new AzureResourceProperty("Resource group", this.SignalResult.ResourceIdentifier.ResourceGroupName),
                    new AzureResourceProperty("Resource type", this.SignalResult.ResourceIdentifier.ResourceType.ToString()),
                    new AzureResourceProperty("Resource name", this.SignalResult.ResourceIdentifier.ResourceName)
                });

            this.PropertiesSectionProperties = new ObservableCollection<SmartSignalResultItemPresentationProperty>(
                this.SignalResult.ResultItemPresentation.Properties
                    .Where(prop => prop.DisplayCategory == ResultItemPresentationSection.Property).ToList());

            this.AnalysisSectionProperties = new ObservableCollection<SmartSignalResultItemPresentationProperty>(
                this.SignalResult.ResultItemPresentation.Properties
                    .Where(prop => prop.DisplayCategory == ResultItemPresentationSection.Analysis
                                   && prop.Value != string.Empty).ToList());

            List<AnalyticsQuery> queries = this.SignalResult.ResultItemPresentation.Properties
                    .Where(prop => prop.DisplayCategory == ResultItemPresentationSection.Chart)
                    .Select(chartItem => new AnalyticsQuery("name", "https://www.nba.com")).ToList();

            this.AnalyticsQuerys = new ObservableCollection<AnalyticsQuery>(queries);

            this.CloseControlCommand = new CommandHandler(() =>
            {
                resultDetailsControlClosed.Invoke();
            });
        }

        #endregion

        #region Binded Properties

        /// <summary>
        /// Gets the signal result.
        /// </summary>
        public SignalResultItem SignalResult
        {
            get
            {
                return this.signalResult;
            }

            private set
            {
                this.signalResult = value;
                this.OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the essentials section's properties.
        /// </summary>
        public ObservableCollection<AzureResourceProperty> EssentialsSectionProperties
        {
            get
            {
                return this.essentialsSectionProperties;
            }

            private set
            {
                this.essentialsSectionProperties = value;
                this.OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the properties section's properties.
        /// </summary>
        public ObservableCollection<SmartSignalResultItemPresentationProperty> PropertiesSectionProperties
        {
            get
            {
                return this.propertiesSectionProperties;
            }

            private set
            {
                this.propertiesSectionProperties = value;
                this.OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the analysis section's properties.
        /// </summary>
        public ObservableCollection<SmartSignalResultItemPresentationProperty> AnalysisSectionProperties
        {
            get
            {
                return this.analysisSectionProperties;
            }

            private set
            {
                this.analysisSectionProperties = value;
                this.OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the App Analytics queries.
        /// </summary>
        public ObservableCollection<AnalyticsQuery> AnalyticsQuerys
        {
            get
            {
                return this.analyticsQueries;
            }

            private set
            {
                this.analyticsQueries = value;
                this.OnPropertyChanged();
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the command that runs the signal.
        /// </summary>
        public CommandHandler CloseControlCommand { get; }

        /// <summary>
        /// Gets a command to open an analytics kusto query in a new browser tab.
        /// </summary>
        public CommandHandler OpenAnalyticsQueryCommand => new CommandHandler(parameter =>
        {
            // Get the query from the parameter
            string query = (string)parameter;

            // Compress it so we can add it to the query parameters
            string compressedQuery;
            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    byte[] queryBtyes = Encoding.UTF8.GetBytes(query);
                    gzipStream.Write(queryBtyes, 0, queryBtyes.Length);
                }

                compressedQuery = Convert.ToBase64String(outputStream.ToArray());
            }

            Console.Write(compressedQuery);

            // Compose the URI
            ////Uri queryDeepLink =
            ////    new Uri($"https://{IntAnalyticsUrl}/subscriptions/{this.Detection.ApplicationIdentifiers.AccountId}/resourcegroups/Scrubber/components/{this.Detection.ApplicationIdentifiers.ApplicationName}?q={compressedQuery}");

            ////// And open it in a new browser
            ////BrowserServices.OpenLinkInBrowser(queryDeepLink);

            Process.Start(new ProcessStartInfo("https://analytics.applicationinsights.io/subscriptions/72993b69-db12-44fc-9a66-9c2005c30513/resourcegroups/Fabrikam/components/fabrikamprod?q=H4sIAAAAAAAAAytKLSxNLS4pVuDlqlEoz0gtSlUoycwFiiTmFijY2SokpudrGGZoAqUBhbSbiSoAAAA%3D"));
        });

        #endregion
    }
}
