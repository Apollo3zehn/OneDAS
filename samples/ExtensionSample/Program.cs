﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneDas.Infrastructure;
using OneDas.Extensibility;
using OneDas.Extensibility.DataGatewaySample;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ExtensionSample
{
    class Program
    {
        /* improvements:
        * - make reading big endian values easier
        * - FrameRateDivider should not be set as it is not used in this example
        */

        static void Main(string[] args)
        {
            var services = new ServiceCollection();

            ConfigureServices(services);

            var provider = services.BuildServiceProvider();
            var extensionFactory = provider.GetRequiredService<IExtensionFactory>();

            extensionFactory.Add(typeof(DataGatewaySampleGateway));

            var settings = new DataGatewaySampleSettings();
            settings.ModuleSet.Add(new OneDasModule(OneDasDataType.FLOAT32, DataDirection.Input, Endianness.LittleEndian, 10));
            settings.Validate();

            Console.CursorVisible = false;

            Task.Run(() =>
            {
                using (var gateway = extensionFactory.BuildLogic<DataGatewaySampleGateway>(settings))
                {
                    gateway.Configure(new DataGatewayContext());

                    while (true)
                    {
                        gateway.UpdateIo(DateTime.Now);

                        var values = MemoryMarshal.Cast<byte, float>(gateway.GetInputBuffer());

                        Console.Clear();

                        for (int i = 0; i < 10; i++)
                        {
                            Console.WriteLine(String.Format(CultureInfo.InvariantCulture, "{0,6:F2}", values[i]));
                        }

                        Thread.Sleep(1000);
                    }
                }
            });

            Console.ReadKey(true);
        }

        static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IExtensionFactory, ExtensionFactory>();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                loggingBuilder.AddConsole();
            });
        }
    }
}
