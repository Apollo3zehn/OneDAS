﻿using OneDas.Buffers;
using OneDas.Extensibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OneDas.DataManagement.Database
{
    [DebuggerDisplay("{Id,nq}")]
    public class CampaignInfo : CampaignElement
    {
        #region "Constructors"

        public CampaignInfo(string id) : base(id, null)
        {
            this.Variables = new List<VariableInfo>();
        }

        private CampaignInfo()
        {
            //
        }

        #endregion

        #region "Properties"

        public DateTime CampaignStart { get; set; }

        public DateTime CampaignEnd { get; set; }

        public List<VariableInfo> Variables { get; set; }

        #endregion

        #region "Methods"

        public List<VariableDescription> ToVariableDescriptions()
        {
            return this.Variables.SelectMany(variable =>
            {
                return variable.Datasets.Select(dataset =>
                {
                    var guid = new Guid(variable.Id);
                    var displayName = variable.Name;
                    var datasetName = dataset.Id;
                    var groupName = variable.Group;
                    var dataType = dataset.DataType;
                    var sampleRate = dataset.GetSampleRate();
                    var unit = variable.Unit;
                    var transferFunctions = variable.TransferFunctions;

                    return new VariableDescription(guid,
                                                   displayName,
                                                   datasetName,
                                                   groupName,
                                                   dataType,
                                                   sampleRate,
                                                   unit,
                                                   transferFunctions,
                                                   BufferType.Simple);
                });
            }).ToList();
        }

        public CampaignInfo ToSparseCampaign(List<DatasetInfo> datasets)
        {
            var campaign = new CampaignInfo(this.Id);
            var variables = datasets.Select(dataset => (VariableInfo)dataset.Parent).Distinct().ToList();

            campaign.Variables = variables.Select(reference =>
            {
                var variable = new VariableInfo(reference.Id, campaign)
                {
                    Name = reference.Name,
                    Group = reference.Group,
                    Unit = reference.Unit,
                    TransferFunctions = reference.TransferFunctions,
                };

                var referenceDatasets = datasets.Where(dataset => (VariableInfo)dataset.Parent == reference);

                variable.Datasets = referenceDatasets.Select(referenceDataset =>
                {
                    return new DatasetInfo(referenceDataset.Id, variable)
                    {
                        DataType = referenceDataset.DataType,
                        IsNative = referenceDataset.IsNative
                    };
                }).ToList();

                return variable;
            }).ToList();

            return campaign;
        }

        public void Merge(CampaignInfo campaign)
        {
            if (this.Id != campaign.Id)
                throw new Exception("The campaign to be merged has a different ID.");

            // merge variables
            List<VariableInfo> newVariables = new List<VariableInfo>();

            foreach (var variable in campaign.Variables)
            {
                var referenceVariable = this.Variables.FirstOrDefault(current => current.Id == variable.Id);

                if (referenceVariable != null)
                    referenceVariable.Merge(variable);
                else
                    newVariables.Add(variable);

                variable.Parent = this;
            }

            this.Variables.AddRange(newVariables);

            // merge other data
            if (this.CampaignStart == DateTime.MinValue)
                this.CampaignStart = campaign.CampaignStart;
            else
                this.CampaignStart = new DateTime(Math.Min(this.CampaignStart.Ticks, campaign.CampaignStart.Ticks));

            if (this.CampaignEnd == DateTime.MinValue)
                this.CampaignEnd = campaign.CampaignEnd;
            else
                this.CampaignEnd = new DateTime(Math.Max(this.CampaignEnd.Ticks, campaign.CampaignEnd.Ticks));
        }

        public override string GetPath()
        {
            return this.Id;
        }

        public override IEnumerable<CampaignElement> GetChilds()
        {
            return this.Variables;
        }

        public override void Initialize()
        {
            base.Initialize();

            foreach (var variable in this.Variables)
            {
                variable.Parent = this;
                variable.Initialize();
            }
        }

        #endregion
    }
}
