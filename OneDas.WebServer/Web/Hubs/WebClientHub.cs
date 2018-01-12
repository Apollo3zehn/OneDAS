﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneDas.Common;
using OneDas.Engine.Core;
using OneDas.Engine.Serialization;
using OneDas.Infrastructure;
using OneDas.Plugin;
using OneDas.WebServer.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace OneDas.WebServer.Web
{
    public class WebClientHub : Hub<IWebClientHub>
    {
        private PluginManager _pluginManager;
        private OneDasEngine _oneDasEngine;
        private WebServerOptions _webServerOptions;

        private ILogger _webServerLogger;

        public WebClientHub(PluginManager pluginManager, OneDasEngine oneDasEngine, IOptions<WebServerOptions> options, ILoggerFactory loggerFactory)
        {
            _pluginManager = pluginManager;
            _oneDasEngine = oneDasEngine;
            _webServerOptions = options.Value;
            _webServerLogger = loggerFactory.CreateLogger("WebServer");
        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (HomeController.LiveViewSubscriptionSet.ContainsKey(this.Context.ConnectionId))
            {
                HomeController.LiveViewSubscriptionSet.Remove(this.Context.ConnectionId);
            }

            return base.OnDisconnectedAsync(exception);
        }

        public Task StartOneDas()
        {
            return Task.Run(() => _oneDasEngine.Start());
        }

        public Task StopOneDas()
        {
            return Task.Run(() => _oneDasEngine.Stop());
        }

        public Task AcknowledgeError()
        {
            return Task.Run(() => _oneDasEngine.AcknowledgeError());
        }

        public Task<string> GetLastError()
        {
            return Task.Run(() => _oneDasEngine.LastError);
        }

        public Task SaveSlimOneDasSettings(SlimOneDasSettings slimOneDasSettings)
        {
            return Task.Run(() =>
            {
                _webServerOptions.OneDasName = slimOneDasSettings.OneDasName;
                _webServerOptions.AspBaseUrl = slimOneDasSettings.AspBaseUrl;
                _webServerOptions.NewBaseDirectoryPath = slimOneDasSettings.BaseDirectoryPath;
                _webServerOptions.Save();

                this.Clients.All.SendSlimOneDasSettings(slimOneDasSettings);
            });
        }

        public Task SaveProject(Project project)
        {
            string fileName;
            string directoryPath;
            string currentFilePath;
            string newFilePath;

            Contract.Requires(project != null);

            return Task.Run(() =>
            {
                project.Validate();

                directoryPath = Path.Combine(_webServerOptions.BaseDirectoryPath, "project");
                fileName = $"{project.Description.CampaignPrimaryGroup}_{project.Description.CampaignSecondaryGroup}_{project.Description.CampaignName}_{project.Description.Guid.ToString()}.json";
                currentFilePath = Path.Combine(directoryPath, fileName);

                try
                {
                    if (File.Exists(currentFilePath))
                    {
                        newFilePath = Path.Combine(_webServerOptions.BaseDirectoryPath, "backup", $"{ DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss") }_{ fileName }");

                        File.Copy(currentFilePath, newFilePath, true);
                    }
                }
                finally
                {
                    currentFilePath = project.Save(currentFilePath);

                    _webServerLogger.LogInformation("project file saved");
                }
            });
        }

        public Task ActivateProject(ProjectDescription projectDescription)
        {
            string filePath;
            Project project;

            return Task.Run(() =>
            {
                projectDescription.Validate();

                // Improve: Make more flexible, renaming of file is impossible like that
                filePath = Path.Combine(_webServerOptions.BaseDirectoryPath, "project", $"{ projectDescription.CampaignPrimaryGroup }_{ projectDescription.CampaignSecondaryGroup }_{ projectDescription.CampaignName }_{ projectDescription.Guid }.json");
                project = ProjectSerializationHelper.Load(filePath);

                _webServerOptions.CurrentProjectFilePath = filePath;
                _webServerOptions.Save();

                _oneDasEngine.ActivateProject(project, 2);

                this.Clients.All.SendActiveProject(project);
            });
        }

        public Task<int> UpdateLiveViewSubscription(IList<Guid> channelHubGuidSet)
        {
            int subscriptionId;
            IList<ChannelHub> channelHubSet;

            return Task.Run(() =>
            {
                try
                {
                    channelHubSet = channelHubGuidSet.Select(channelHubGuid => _oneDasEngine.Project.ActiveChannelHubSet.First(channelHub => channelHub.Guid == channelHubGuid)).ToList();
                }
                catch (Exception)
                {
                    throw new Exception(ErrorMessage.Broadcaster_ChannelHubNotFound);
                }

                subscriptionId = HomeController.GetNextSubscriptionId();
                HomeController.LiveViewSubscriptionSet[this.Context.ConnectionId] = (subscriptionId, channelHubSet);

                return subscriptionId;
            });
        }

        public Task<string> GetPluginStringResource(string pluginId, string resourceName)
        {
            return Task.Run(() =>
            {
                return _pluginManager.GetStringResource(pluginId, resourceName);
            });
        }

        public Task<IEnumerable<ProjectDescription>> GetProjectDescriptions()
        {
            return Task.Run(() =>
            {
                IEnumerable<string> filePathSet;
                IList<ProjectDescription> projectDescriptionSet;

                filePathSet = Directory.GetFiles(Path.Combine(_webServerOptions.BaseDirectoryPath, "project"), "*.json");
                projectDescriptionSet = new List<ProjectDescription>();

                foreach (string filePath in filePathSet)
                {
                    try
                    {
                        projectDescriptionSet.Add(SerializationHelper.GetProjectDescriptonFromFile(filePath));
                    }
                    catch (Exception)
                    {
                        //
                    }
                }

                return (IEnumerable<ProjectDescription>)projectDescriptionSet;
            });
        }

        public Task<Project> OpenProject(ProjectDescription projectDescription)
        {
            return Task.Run(() =>
            {
                // Improve: Make more flexible, renaming of file is impossible like that
                Project project = ProjectSerializationHelper.Load(Path.Combine(_webServerOptions.BaseDirectoryPath, "project", $"{projectDescription.CampaignPrimaryGroup}_{projectDescription.CampaignSecondaryGroup}_{projectDescription.CampaignName}_{projectDescription.Guid}.json"));

                return project;
            });
        }

        public Task<ActionResponse> RequestAction(ActionRequest actionRequest)
        {
            return Task.Run(() =>
            {
                _pluginManager.HandleActionRequest();
            });
        }

        public Task<AppModel> GetAppModel()
        {
            IList<PluginIdentificationAttribute> dataGatewayPluginIdentificationSet;
            IList<PluginIdentificationAttribute> dataWriterPluginIdentificationSet;

            dataGatewayPluginIdentificationSet = _pluginManager.Get<DataGatewayPluginSettingsBase>().Select(dataGatewaySettingsType =>
            {
                PluginIdentificationAttribute pluginIdentificationAttribute;

                pluginIdentificationAttribute = dataGatewaySettingsType.GetFirstAttribute<PluginIdentificationAttribute>();
                pluginIdentificationAttribute.ProductVersion = FileVersionInfo.GetVersionInfo(dataGatewaySettingsType.Assembly.Location).ProductVersion;

                return pluginIdentificationAttribute;
            }).ToList();

            dataWriterPluginIdentificationSet = _pluginManager.Get<DataWriterPluginSettingsBase>().Select(dataWriterSettingsType =>
            {
                PluginIdentificationAttribute pluginIdentificationAttribute;

                pluginIdentificationAttribute = dataWriterSettingsType.GetFirstAttribute<PluginIdentificationAttribute>();
                pluginIdentificationAttribute.ProductVersion = FileVersionInfo.GetVersionInfo(dataWriterSettingsType.Assembly.Location).ProductVersion;

                return pluginIdentificationAttribute;
            }).ToList();

            return Task.Run(() =>
            {
                return new AppModel(
                    activeProject: _oneDasEngine.Project,
                    clientSet: new List<string>() { "Horst", "Köhler" },
                    dataGatewayPluginIdentificationSet: dataGatewayPluginIdentificationSet,
                    dataWriterPluginIdentificationSet: dataWriterPluginIdentificationSet,
                    lastError: _oneDasEngine.LastError,
                    oneDasState: _oneDasEngine.OneDasState,
                    slimOneDasSettings: new SlimOneDasSettings
                    {
                        OneDasName = _webServerOptions.OneDasName,
                        AspBaseUrl = _webServerOptions.AspBaseUrl,
                        BaseDirectoryPath = _webServerOptions.BaseDirectoryPath
                    });
            });
        }

        public Task<Project> CreateProject(string campaignPrimaryGroup, string campaignSecondaryGroup, string configurationName)
        {
            return Task.Run(() =>
            {
                IList<DataGatewayPluginSettingsBase> dataGatewayPluginSettingsSet;
                IList<DataWriterPluginSettingsBase> dataWriterPluginSettingsSet;

                dataGatewayPluginSettingsSet = _pluginManager.Get<DataGatewayPluginSettingsBase>().
                        Where(pluginSettingsType => pluginSettingsType.GetFirstAttribute<PluginIdentificationAttribute>().Id == "EtherCAT").
                        Select(pluginSettingsType => _pluginManager.BuildSettings<DataGatewayPluginSettingsBase>(pluginSettingsType)).ToList();

                dataWriterPluginSettingsSet = _pluginManager.Get<DataWriterPluginSettingsBase>().
                        Where(pluginSettingsType => pluginSettingsType.GetFirstAttribute<PluginIdentificationAttribute>().Id == "HDF").
                        Select(pluginSettingsType => _pluginManager.BuildSettings<DataWriterPluginSettingsBase>(pluginSettingsType)).ToList();

                return new Project(
                    campaignPrimaryGroup, 
                    campaignSecondaryGroup, 
                    configurationName,
                    dataGatewayPluginSettingsSet,
                    dataWriterPluginSettingsSet);
            });
        }

        public Task<DataGatewayPluginSettingsBase> CreateDataGatewaySettings(string pluginName)
        {
            return Task.Run(() =>
            {
                return _pluginManager.BuildSettings<DataGatewayPluginSettingsBase>(pluginName);
            });
        }

        public Task<DataWriterPluginSettingsBase> CreateDataWriterSettings(string pluginName)
        {
            return Task.Run(() =>
            {
                return _pluginManager.BuildSettings<DataWriterPluginSettingsBase>(pluginName);
            });
        }
    }
}