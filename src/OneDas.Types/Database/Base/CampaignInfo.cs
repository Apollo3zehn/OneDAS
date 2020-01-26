﻿using OneDas.DataStorage;
using OneDas.Extensibility;
using OneDas.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace OneDas.Database
{
    [DebuggerDisplay("{Name,nq}")]
    [DataContract]
    public class CampaignInfo : InfoBase
    {
        #region "Constructors"

        public CampaignInfo(string name) : base(name, null)
        {
            this.ChunkDataset = new DatasetInfo("is_chunk_completed_set", this);
            this.Variables = new List<VariableInfo>();
        }

        #endregion

        #region "Properties"

        [DataMember]
        public DatasetInfo ChunkDataset { get; set; }

        [DataMember]
        public List<VariableInfo> Variables { get; set; }

        #endregion

        #region "Methods"

        public List<VariableDescription> ToVariableDescriptions()
        {
            return this.Variables.SelectMany(variable =>
            {
                return variable.Datasets.Select(dataset =>
                {
                    var guid = new Guid(variable.Name);
                    var displayName = variable.VariableNames.Last();
                    var datasetName = dataset.Name;
                    var groupName = variable.VariableGroups.Last();
                    var dataType = dataset.DataType;
                    var sampleRate = new SampleRateContainer(dataset.Name);
                    var unit = variable.Units.Last();
                    var transferFunctions = variable.TransferFunctions;

                    return new VariableDescription(guid, displayName, datasetName, groupName, dataType, sampleRate, unit, transferFunctions, DataStorageType.Simple);
                });
            }).ToList();
        }

        public CampaignInfo ToSparseCampaign(Dictionary<string, List<string>> variableMap)
        {
            var campaign = new CampaignInfo(this.Name);

            campaign.Variables = variableMap.Select(variableEntry =>
            {
                var referenceVariable = this.Variables.FirstOrDefault(currentVariable => currentVariable.Name == variableEntry.Key);

                if (referenceVariable is null)
                    throw new KeyNotFoundException($"The requested variable '{referenceVariable.Name}' is unknown.");

                var variable = new VariableInfo(referenceVariable.Name, campaign)
                {
                    TransferFunctions = referenceVariable.TransferFunctions,
                    Units = referenceVariable.Units,
                    VariableGroups = referenceVariable.VariableGroups,
                    VariableNames = referenceVariable.VariableNames
                };

                variable.Datasets = variableEntry.Value.Select(datasetName =>
                {
                    var referenceDataset = referenceVariable.Datasets.FirstOrDefault(currentDataset => currentDataset.Name == datasetName);

                    if (referenceDataset is null)
                        throw new KeyNotFoundException($"The requested dataset '{referenceDataset.Name}' is unknown.");

                    return new DatasetInfo(referenceDataset.Name, variable) { DataType = referenceDataset.DataType };
                }).ToList();

                return variable;
            }).ToList();

            return campaign;
        }

        public override string GetPath()
        {
            return this.Name;
        }

        public override IEnumerable<InfoBase> GetChilds()
        {
            return this.Variables;
        }

        #endregion
    }
}