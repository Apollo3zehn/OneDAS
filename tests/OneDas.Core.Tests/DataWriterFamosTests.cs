using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneDas.Buffers;
using OneDas.Extensibility;
using OneDas.Extension.Famos;
using OneDas.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace OneDas.Core.Tests
{
    public class DataWriterFamosTests
    {
        [Fact]
        public void FamosWriterCreatesDatFile()
        {
            // Arrange
            var services = new ServiceCollection();

            ConfigureServices(services);

            var provider = services.BuildServiceProvider();
            var dataWriter = provider.GetRequiredService<FamosWriter>();

            var projectGuid = Guid.NewGuid();
            var dataDirectoryPath = Path.Combine(Path.GetTempPath(), projectGuid.ToString());

            Directory.CreateDirectory(dataDirectoryPath);

            var projectDescription = new OneDasProjectDescription(projectGuid, 1, "a", "b", "c");
            var customMetadataEntrySet = new List<CustomMetadataEntry>();
            var dataWriterContext = new DataWriterContext("OneDAS", dataDirectoryPath, projectDescription, customMetadataEntrySet);

            var variableDescriptionSet = new List<VariableDescription>()
            {
                this.CreateVariableDescription("Var1", "Group1", OneDasDataType.FLOAT64, new SampleRateContainer(8640000), "Unit1"),
                this.CreateVariableDescription("Var2", "Group2", OneDasDataType.FLOAT64, new SampleRateContainer(8640000), "Unit2"),
                this.CreateVariableDescription("Var3", "Group1", OneDasDataType.FLOAT64, new SampleRateContainer(86400), "Unit2"),
            };

            var currentDate = new DateTime(2019, 1, 1, 15, 0, 0);
            var period = TimeSpan.FromMinutes(1);

            // Act
            dataWriter.Configure(dataWriterContext, variableDescriptionSet);

            for (int i = 0; i < 9; i++)
            {
                var buffers = variableDescriptionSet.Select(current =>
                {
                    var length = (int)current.SampleRate.SamplesPerDay / 1440;
                    var offset = length * i;
                    var data = Enumerable.Range(offset, length).Select(value => value * 0 + (double)i + 1).ToArray();

                    return BufferUtilities.CreateSimpleBuffer(data);
                }).ToList();

                dataWriter.Write(currentDate, period, buffers.Cast<IBuffer>().ToList());
                currentDate += period;
            }

            dataWriter.Dispose();

            // Assert
        }

        private VariableDescription CreateVariableDescription(string variableName, string group, OneDasDataType dataType, SampleRateContainer sampleRate, string unit)
        {
            var guid = Guid.NewGuid();
            var datasetName = sampleRate.ToUnitString();
            var transferFunctionSet = new List<TransferFunction>();

            return new VariableDescription(guid, variableName, datasetName, group, dataType, sampleRate, unit, transferFunctionSet, BufferType.Simple);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(current => new FamosWriter(new FamosSettings() { FileGranularity = FileGranularity.Minute_10 }, NullLogger.Instance));
        }
    }
}