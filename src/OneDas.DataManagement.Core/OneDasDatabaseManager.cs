﻿using OneDas.DataManagement.Database;
using OneDas.DataManagement.Extensibility;
using OneDas.DataManagement.Extensions;
using OneDas.Extensibility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace OneDas.DataManagement
{
    /* 
     * Algorithm:
     * *******************************************************************************
     * 01. load database.json (this.Database)
     * 02. load and instantiate data reader extensions (_rootPathToDataReaderMap)
     * 03. call Update() method
     * 04.   for each data reader in _rootPathToDataReaderMap
     * 05.       get campaign names
     * 06.           for each campaign name
     * 07.               find campaign container in current database or create new one
     * 08.               get an up-to-date campaign instance from the data reader
     * 09.               merge both campaigns
     * 10. save updated database
     * *******************************************************************************
     */

    public class OneDasDatabaseManager
    {
        #region Fields

        private Dictionary<string, DataReaderExtensionBase> _rootPathToDataReaderMap;

        #endregion

        #region Constructors

        public OneDasDatabaseManager()
        {
            // database
            this.Database = new OneDasDatabase();

            // config
            var filePath = Path.Combine(Environment.CurrentDirectory, "config.json");

            if (!File.Exists(filePath))
            {
                this.Config = new OneDasDatabaseConfig();
            }
            else
            {
                var jsonString = File.ReadAllText(filePath);
                this.Config = JsonSerializer.Deserialize<OneDasDatabaseConfig>(jsonString);
            }

            _rootPathToDataReaderMap = this.LoadDataReader(this.Config.RootPathToDataReaderIdMap);

            this.Update();
        }

        #endregion

        #region Properties

        public OneDasDatabase Database { get; }

        public OneDasDatabaseConfig Config { get; }

        public DataReaderExtensionBase AggregationDataReader { get; private set; }

        #endregion

        #region Methods

        public void Update()
        {
            var dataReaders = _rootPathToDataReaderMap
                                .Select(entry => entry.Value)
                                .Concat(new DataReaderExtensionBase[] { this.AggregationDataReader })
                                .ToList();

            foreach (var dataReader in dataReaders)
            {
                try
                {
                    var isNativeDataReader = dataReader != this.AggregationDataReader;
                    var campaignNames = dataReader.GetCampaignNames();

                    foreach (var campaignName in campaignNames)
                    {
                        // find campaign container or create a new one
                        var container = this.Database.CampaignContainers.FirstOrDefault(container => container.Name == campaignName);

                        if (container == null)
                        {
                            container = new CampaignContainer(campaignName, dataReader.RootPath);
                            this.Database.CampaignContainers.Add(container);

                            // try to load campaign meta data
                            var filePath = Path.Combine(Environment.CurrentDirectory, "META", $"{campaignName.TrimStart('/').Replace('/', '_')}.json");

                            CampaignMetaInfo campaignMeta;

                            if (File.Exists(filePath))
                            {
                                var jsonString = File.ReadAllText(filePath);
                                campaignMeta = JsonSerializer.Deserialize<CampaignMetaInfo>(jsonString);
                            }
                            else
                            {
                                campaignMeta = new CampaignMetaInfo(campaignName);
                                var jsonString = JsonSerializer.Serialize(campaignMeta, new JsonSerializerOptions() { WriteIndented = true });
                                File.WriteAllText(filePath, jsonString);
                            }

                            if (string.IsNullOrWhiteSpace(campaignMeta.ShortDescription))
                                campaignMeta.ShortDescription = "<no description available>";

                            if (string.IsNullOrWhiteSpace(campaignMeta.LongDescription))
                                campaignMeta.LongDescription = "<no description available>";

                            container.CampaignMeta = campaignMeta;
                        }

                        // ensure that found campaign container root path matches that of the data reader
                        if (isNativeDataReader)
                        {
                            if (dataReader.RootPath != container.RootPath) 
                                throw new Exception("The data reader root path does not match the root path of the campaign data stored in the database.");
                        }

                        // get up-to-date campaign from data reader
                        var campaign = dataReader.GetCampaign(campaignName);

                        // if data reader is for aggregation data, update the dataset`s flag
                        if (!isNativeDataReader)
                        {
                            var datasets = campaign.Variables.SelectMany(variable => variable.Datasets).ToList();

                            foreach (var dataset in datasets)
                            {
                                dataset.IsNative = false;
                            }

                            campaign.ChunkDataset.IsNative = false;
                        }

                        //
                        container.Campaign.Merge(campaign);
                        container.CampaignMeta.Purge();
                    }
                }
                finally
                {
                    dataReader.Dispose();
                }
            }

            this.Save(this.Config);
        }

        public DataReaderExtensionBase GetNativeDataReader(string campaignName)
        {
            var container = this.Database.CampaignContainers.FirstOrDefault(container => container.Name == campaignName);

            if (container == null)
                throw new KeyNotFoundException("The requested campaign could not be found.");

            if (!_rootPathToDataReaderMap.TryGetValue(container.RootPath, out var dataReader))
                throw new KeyNotFoundException("The requested data reader could not be found.");

            return dataReader;
        }

        public List<CampaignInfo> GetCampaigns()
        {
            return this.Database.CampaignContainers.Select(container => container.Campaign).ToList();
        }

        private void Save(OneDasDatabaseConfig config)
        {
            var filePath = Path.Combine(Environment.CurrentDirectory, "config.json");
            var jsonString = JsonSerializer.Serialize(config, new JsonSerializerOptions() { WriteIndented = true });

            File.WriteAllText(filePath, jsonString);
        }

        private Dictionary<string, DataReaderExtensionBase> LoadDataReader(Dictionary<string, string> rootPathToDataReaderIdMap)
        {
            var extensionDirectoryPath = Path.Combine(Environment.CurrentDirectory, "EXTENSION");

            var extensionFilePaths = Directory.EnumerateFiles(extensionDirectoryPath, "*.deps.json", SearchOption.AllDirectories)
                                              .Select(filePath => filePath.Replace(".deps.json", ".dll")).ToList();

            var idToDataReaderTypeMap = new Dictionary<string, Type>();
            var types = new List<Type>();

            // load assemblies
            foreach (var filePath in extensionFilePaths)
            {
                var loadContext = new ExtensionLoadContext(filePath);
                var assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(filePath));
                var assembly = loadContext.LoadFromAssemblyName(assemblyName);

                types.AddRange(this.ScanAssembly(assembly));
            }

#warning Improve this.
            // add additional data reader
            types.Add(typeof(HdfDataReader));
            types.Add(typeof(InMemoryDataReader));

            // get ID for each extension
            foreach (var type in types)
            {
                var attribute = type.GetFirstAttribute<ExtensionIdentificationAttribute>();
                idToDataReaderTypeMap[attribute.Id] = type;
            }

            // instantiate aggregation data reader
            this.AggregationDataReader = new HdfDataReader(Environment.CurrentDirectory);

            // instantiate extensions
            return rootPathToDataReaderIdMap.ToDictionary(entry => entry.Key, entry =>
            {
                var rootPath = entry.Key;
                var dataReaderId = entry.Value;

                if (!idToDataReaderTypeMap.TryGetValue(dataReaderId, out var type))
                    throw new Exception($"No data reader extension with ID '{dataReaderId}' could be found.");

                return (DataReaderExtensionBase)Activator.CreateInstance(type, rootPath);
            });
        }

        private List<Type> ScanAssembly(Assembly assembly)
        {
            return assembly.ExportedTypes.Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(DataReaderExtensionBase))).ToList();
        }

        #endregion
    }
}
