﻿using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using OneDas.Core.Engine;
using System;
using System.Diagnostics;
using System.Net;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;

namespace OneDas.WebServer.Shell
{
    public class OneDasConsole
    {
        #region "Fields"

        private Timer _timer_UpdateConsole;
        private object _syncLock_UpdateConsole;

        private HubConnection _consoleHubClient;
        private WebServerOptions _webServerOptions;

        private bool _isConnected = false;

        #endregion

        #region "Contructors"

        public OneDasConsole(IOptions<WebServerOptions> options)
        {
            _webServerOptions = options.Value;
            _syncLock_UpdateConsole = new object();

            Console.CursorVisible = false;
        }

        #endregion

        #region "Methods"

        public Task RunAsync(bool isHosting)
        {
            return Task.Run(() =>
            {
                // SignalR connection
                _consoleHubClient = this.BuildHubConnection();

                _consoleHubClient.Closed += e =>
                {
                    return Task.Run(() =>
                    {
                        _isConnected = false;
                        this.ResetConsole();
                    });
                };

                Task.Run(async () =>
                {
                    while (true)
                    {
                        if (!_isConnected)
                        {
                            try
                            {
                                _consoleHubClient.StartAsync().Wait();
                                _isConnected = true;

                                this.ResetConsole();
                            }
                            catch
                            {
                                await Task.Delay(TimeSpan.FromSeconds(4));
                            }
                        }

                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                });

                // timer
                _timer_UpdateConsole = new Timer(new TimeSpan(0, 0, 1).TotalMilliseconds)
                {
                    AutoReset = true,
                    Enabled = true
                };

                _timer_UpdateConsole.Elapsed += _timer_UpdateConsole_Elapsed;

                // to serve or not to serve?
                Console.Title = "OneDAS Core";

                if (!isHosting)
                {
                    Console.Title += " (remote control)";
                }

                // wait for user input (loop)
                this.ResetConsole();

                while (true)
                {
                    Console.ReadKey(true);
                }
            });
        }

        private void WriteColoredLine(string text, ConsoleColor color)
        {
            this.WriteColored(text, color);
            Console.WriteLine();
        }

        private void WriteColored(string text, ConsoleColor color)
        {
            if (color == ConsoleColor.White)
            {
                color = Console.ForegroundColor;
            }

            ConsoleColor currentColor = Console.ForegroundColor;

            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = currentColor;
        }

        private void ResetConsole()
        {
            lock (_syncLock_UpdateConsole)
            {
                int offset = 38;

                // frame
                Console.Clear();

                Console.Write($"+================================"); Console.Write($" OneDAS Core "); Console.WriteLine($"================================+");
                Console.WriteLine($"|                                                                             |");
                Console.WriteLine($"|                                     |                                       |");
                Console.WriteLine($"|                                     |                                       |");
                Console.WriteLine($"|                                     |                                       |");
                Console.WriteLine($"|                                     |                                       |");
                Console.WriteLine($"|                                                                             |");
                Console.WriteLine($"+=============================================================================+");

                // text
                Console.SetCursorPosition(2, 2);
                Console.Write("Instance name:");

                Console.SetCursorPosition(2, 3);
                Console.Write("Status:");

                Console.SetCursorPosition(2, 4);
                Console.Write("Process priority:");

                Console.SetCursorPosition(2, 5);
                Console.Write("Windows service:");

                // numbers
                Console.SetCursorPosition(2 + offset, 2);
                Console.Write("Late by:");

                Console.SetCursorPosition(2 + offset, 3);
                Console.Write("Cycle time:");

                Console.SetCursorPosition(2 + offset, 4);
                Console.Write("Timer drift:");

                Console.SetCursorPosition(2 + offset, 5);
                Console.Write("Processor time:");

                if (!_isConnected)
                {
                    Console.SetCursorPosition(31, 9);
                    this.WriteColoredLine("connection lost", ConsoleColor.Red);
                }

                Console.SetCursorPosition(0, 11);
                Console.WriteLine("Press CTRL+C to shut down the application.");
            }
        }

        private async void Update()
        {
            OneDasPerformanceInformation performanceInformation;

            if (!_isConnected)
            {
                return;
            }

            try
            {
                performanceInformation = await _consoleHubClient.InvokeAsync<OneDasPerformanceInformation>("GetPerformanceInformation");

                lock (_syncLock_UpdateConsole)
                {
                    int offset = 39;

                    // text
                    Console.SetCursorPosition(37 - _webServerOptions.OneDasName.Length, 2);
                    Console.Write(_webServerOptions.OneDasName);

                    Console.SetCursorPosition(19, 3);

                    switch (performanceInformation.OneDasState)
                    {
                        case OneDasState.Run:
                            Console.Write($"{performanceInformation.OneDasState,18}");
                            break;
                        default:
                            this.WriteColored($"{performanceInformation.OneDasState,18}", ConsoleColor.Red);
                            break;
                    }

                    Console.SetCursorPosition(26, 4);
                    this.WriteColored($"{performanceInformation.ProcessPriorityClass,11}", performanceInformation.ProcessPriorityClass == ProcessPriorityClass.RealTime ? ConsoleColor.White : ConsoleColor.Red);

                    Console.SetCursorPosition(22, 5);
                    ServiceControllerStatus oneDasServiceStatus = BasicBootloader.GetOneDasServiceStatus();
                    string text = oneDasServiceStatus == 0 ? "NotFound" : oneDasServiceStatus.ToString();
                    this.WriteColored($"{text,15}", oneDasServiceStatus == ServiceControllerStatus.Running ? ConsoleColor.White : ConsoleColor.Red);

                    // numbers
                    Console.SetCursorPosition(30 + offset, 2);
                    Console.Write($"{performanceInformation.LateBy,5:0.0} ms");

                    Console.SetCursorPosition(30 + offset, 3);
                    Console.Write($"{performanceInformation.CycleTime,5:0.0} ms");

                    Console.SetCursorPosition(28 + offset, 4);
                    Console.Write($"{(int)(performanceInformation.TimerDrift / 1000),7} µs");

                    Console.SetCursorPosition(28 + offset, 5);
                    Console.Write($"{performanceInformation.CpuTime,7:0} %");

                    Console.SetCursorPosition(0, 11);
                }
            }
            catch
            {
                //
            }
        }

        private HubConnection BuildHubConnection()
        {
            UriBuilder uriBuilder;

            uriBuilder = new UriBuilder(_webServerOptions.AspBaseUrl)
            {
                Host = IPAddress.Loopback.ToString(),
                Path = _webServerOptions.ConsoleHubName
            };

            return new HubConnectionBuilder()
                 .WithUrl(uriBuilder.ToString())
                 .Build();
        }

        #endregion

        #region "Callbacks"

        private void _timer_UpdateConsole_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Update();
        }

        #endregion
    }
}