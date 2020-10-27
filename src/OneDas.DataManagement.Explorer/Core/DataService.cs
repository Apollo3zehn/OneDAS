﻿using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OneDas.DataManagement.Database;
using OneDas.Infrastructure;
using OneDas.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace OneDas.DataManagement.Explorer.Core
{
    public class DataService
    {
        #region Fields

        private ILogger _logger;
        private OneDasDatabaseManager _databaseManager;
        private OneDasExplorerStateManager _stateManager;
        private SignInManager<IdentityUser> _signInManager;
        private OneDasExplorerOptions _options;

        #endregion

        #region Constructors

        public DataService(OneDasExplorerStateManager stateManager,
                           OneDasDatabaseManager databaseManager,
                           SignInManager<IdentityUser> signInManager,
                           ILoggerFactory loggerFactory,
                           OneDasExplorerOptions options)
        {
            _stateManager = stateManager;
            _databaseManager = databaseManager;
            _signInManager = signInManager;
            _logger = loggerFactory.CreateLogger("OneDAS Explorer");
            _options = options;

            this.Progress = new Progress<ProgressUpdatedEventArgs>();
        }

        #endregion

        #region Properties

        public Progress<ProgressUpdatedEventArgs> Progress { get; }

        #endregion

        #region Methods

        public Task<DataAvailabilityStatistics> GetDataAvailabilityStatisticsAsync(string projectId, DateTime begin, DateTime end)
        {
            return Task.Run(() =>
            {
                _stateManager.CheckState();

                if (!Utilities.IsProjectAccessible(_signInManager.Context.User, projectId, _databaseManager.Config.RestrictedProjects))
                    throw new UnauthorizedAccessException($"The current user is not authorized to access project '{projectId}'.");

                using var dataReader = _databaseManager.GetNativeDataReader(projectId);
                return dataReader.GetDataAvailabilityStatistics(projectId, begin, end);
            });
        }

        public Task<string> ExportDataAsync(IPAddress remoteIpAddress,
                                            ExportConfiguration exportConfig,
                                            List<DatasetInfo> datasets,
                                            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                _stateManager.CheckState();

                if (!datasets.Any() || exportConfig.Begin == exportConfig.End)
                    return string.Empty;

                var sampleRate = new SampleRateContainer(exportConfig.SampleRate);

                // log
                string userName;

                if (_signInManager.Context.User.Identity.IsAuthenticated)
                    userName = _signInManager.Context.User.Identity.Name;
                else
                    userName = "anonymous";

                var message = $"User '{userName}' ({remoteIpAddress}) exports data: {exportConfig.Begin.ToString("yyyy-MM-ddTHH:mm:ssZ")} to {exportConfig.End.ToString("yyyy-MM-ddTHH:mm:ssZ")} ... ";
                _logger.LogInformation(message);

                // zip file
                var zipFilePath = Path.Combine(_options.ExportDirectoryPath, $"OneDAS_{exportConfig.Begin.ToString("yyyy-MM-ddTHH-mm")}_{sampleRate.ToUnitString(underscore: true)}_{Guid.NewGuid().ToString().Substring(0, 8)}.zip");

                // sample rate
                try
                {
                    // convert datasets into projects
                    var projectNames = datasets.Select(dataset => dataset.Parent.Parent.Id).Distinct();
                    var projectContainers = _databaseManager.Database.ProjectContainers
                        .Where(projectContainer => projectNames.Contains(projectContainer.Id));

                    var sparseProjects = projectContainers.Select(projectContainer =>
                    {
                        var currentDatasets = datasets.Where(dataset => dataset.Parent.Parent.Id == projectContainer.Id).ToList();
                        return projectContainer.ToSparseProject(currentDatasets);
                    });

                    // security check
                    foreach (var sparseProject in sparseProjects)
                    {
                        if (!Utilities.IsProjectAccessible(_signInManager.Context.User, sparseProject.Id, _databaseManager.Config.RestrictedProjects))
                            throw new UnauthorizedAccessException($"The current user is not authorized to access project '{sparseProject.Id}'.");
                    }

                    // start
                    var blockSizeLimit = 50 * 1000 * 1000UL;

                    using var dataExporter = new DataExporter(zipFilePath, exportConfig, blockSizeLimit);
                    dataExporter.Progress += this.OnProgress;

                    try
                    {
                        foreach (var sparseProject in sparseProjects)
                        {
                            using var nativeDataReader = _databaseManager.GetNativeDataReader(sparseProject.Id);
                            using var aggregationDataReader = _databaseManager.GetAggregationDataReader();

                            var projectSettings = new ProjectSettings(sparseProject, nativeDataReader, aggregationDataReader);

                            if (!dataExporter.WriteZipFileProjectEntry(projectSettings, cancellationToken))
                                return string.Empty;
                        }
                    }
                    finally
                    {
                        dataExporter.Progress -= this.OnProgress;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.GetFullMessage());
                    throw;
                }

                _logger.LogInformation($"{message} Done.");

                return $"export/{Path.GetFileName(zipFilePath)}";
            }, cancellationToken);
        }

        private void OnProgress(object sender, ProgressUpdatedEventArgs e)
        {
            ((IProgress<ProgressUpdatedEventArgs>)this.Progress).Report(e);
        }

        #endregion
    }
}
