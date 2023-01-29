using iRacingSDK;
using OpenRGB.NET;
using OpenRGB.NET.Enums;
using OpenRGB.NET.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IRacingOpenRGB
{
    public class ConnectionClientWrapper : IDisposable
    {
        public const string DefaultRGBClientName = "IRacing RGB Client";
        public iRacingConnection RacingConnection { get; init; }
        public Action<DataSample> IRacingNewSessionDataHandler
        {
            get { return _iRacingNewSessionDataHandler; }
            set
            {
                if (RacingConnection != null)
                {
                    if (IRacingNewSessionDataHandler != null)
                        RacingConnection.NewSessionData -= _iRacingNewSessionDataHandler;

                    if (value != null)
                        RacingConnection.NewSessionData += value;
                }

                _iRacingNewSessionDataHandler = value;
            }
        }
        private Action<DataSample> _iRacingNewSessionDataHandler;

        private Task _telemetryLoopTask;
        private CancellationTokenSource _cancellationTokenSource;

        public string RGBClientName { get; set; }
        public OpenRGBClient RGBClient { get; init; }

        public Device[] RGBDevices { get; init; }

        private bool _disposed = false;

        public ConnectionClientWrapper(string rgbClientName = DefaultRGBClientName)
        {
            RacingConnection = new iRacingConnection();

            IRacingNewSessionDataHandler = OnNewSessionData;

            RGBClientName = rgbClientName;
            RGBClient = new OpenRGBClient(name: rgbClientName, autoconnect: true, timeout: 1000);

            RGBDevices = RGBClient.GetAllControllerData();
            //var devices = RGBClient.GetAllControllerData();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                RacingConnection.NewSessionData -= IRacingNewSessionDataHandler;
                RGBClient.Dispose();
                _telemetryLoopTask?.Dispose();

                _disposed = true;
            }
        }

        public bool CheckConnections()
        {
            Console.WriteLine(
                RGBClient.Connected ?
                    $"'{RGBClientName}' RGB client connected" :
                    $"Could not connect '{RGBClientName}' RGB client");

            Console.WriteLine(
                RacingConnection.IsConnected ?
                    $"Connected to iRacing" :
                    "Could not connect to iRacing");

            return RGBClient.Connected && RacingConnection.IsConnected;
        }

        public void StartTelemetryLoop()
        {
            if (_telemetryLoopTask is { IsCanceled: false })
                return;

            var cancellationTokenSource = new CancellationTokenSource();

            _telemetryLoopTask = Task.Factory.StartNew(() =>
            {
                if (cancellationTokenSource.IsCancellationRequested)
                    return;

                Console.WriteLine("Telemetry loop started");

                foreach (var data in RacingConnection.GetDataFeed()
                   .WithCorrectedPercentages()
                   .WithCorrectedDistances()
                   .WithPitStopCounts())
                {
                    if (cancellationTokenSource.IsCancellationRequested)
                        return;

                    SetRGB(data.Telemetry.SessionFlags);

                    Console.WriteLine(data.Telemetry.SessionFlags.ToString());
                    Console.WriteLine("RGB set by loop");

                    if (cancellationTokenSource.IsCancellationRequested)
                        return;

                    //Trace.WriteLine(data.SessionData.Raw);

                    //System.Diagnostics.Debugger.Break();
                }
            }, cancellationTokenSource.Token);

            _cancellationTokenSource = cancellationTokenSource;
        }

        public void StopTelemetryLoop()
        {
            _cancellationTokenSource?.Cancel();
            _telemetryLoopTask = null;

            Console.WriteLine("Telemetry loop stopped");
        }

        public void OnNewSessionData(DataSample dataSample)
        {
            try
            {
                var flag = dataSample.Telemetry.SessionFlags;
                SetRGB(flag);
                Console.WriteLine("RGB set by event");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void SetRGB(SessionFlags flag)
        {
            foreach (var device in RGBDevices)
            {
                if (flag.HasFlag(SessionFlags.blue))
                {
                    for (int i = 0; i < RGBDevices.Length; i++)
                    {
                        for (int j = 0; j < RGBDevices[i].Modes.Length; j++)
                        {
                            var mode = RGBDevices[i].Modes[j];
                            var len = (int)mode.ColorMax;
                            RGBClient.SetMode(i, 2, speed: null, direction: null, colors: null);
                            RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, len).Select(_ => new Color(0, 0, 255)).ToArray());
                        }
                    }
                }
                else if (flag.HasFlag(SessionFlags.green))
                {
                    for (int i = 0; i < RGBDevices.Length; i++)
                    {
                        for (int j = 0; j < RGBDevices[i].Modes.Length; j++)
                        {
                            var mode = RGBDevices[i].Modes[j];
                            var len = (int)mode.ColorMax;
                            RGBClient.SetMode(i, 4, speed: null, direction: null, colors: null);
                            RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, len).Select(_ => new Color(0, 255, 0)).ToArray());
                        }
                    }
                }
                else if (flag.HasFlag(SessionFlags.greenHeld))
                {
                    for (int i = 0; i < RGBDevices.Length; i++)
                    {
                        for (int j = 0; j < RGBDevices[i].Modes.Length; j++)
                        {
                            var mode = RGBDevices[i].Modes[j];
                            var len = (int)mode.ColorMax;
                            RGBClient.SetMode(i, 4, speed: null, direction: null, colors: null);
                            RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, len).Select(_ => new Color(0, 255, 0)).ToArray());
                        }
                    }
                }
                else if (flag.HasFlag(SessionFlags.caution))
                {
                    for (int i = 0; i < RGBDevices.Length; i++)
                    {
                        for (int j = 0; j < RGBDevices[i].Modes.Length; j++)
                        {
                            var mode = RGBDevices[i].Modes[j];
                            var len = (int)mode.ColorMax;
                            RGBClient.SetMode(i, 2, speed: null, direction: null, colors: null);
                            RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, len).Select(_ => new Color(255, 255, 0)).ToArray());
                        }
                    }
                }
                else if (flag.HasFlag(SessionFlags.cautionWaving))
                {
                    for (int i = 0; i < RGBDevices.Length; i++)
                    {
                        for (int j = 0; j < RGBDevices[i].Modes.Length; j++)
                        {
                            var mode = RGBDevices[i].Modes[j];
                            var len = (int)mode.ColorMax;
                            RGBClient.SetMode(i, 4, speed: null, direction: null, colors: null);
                            RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, len).Select(_ => new Color(255, 255, 0)).ToArray());
                        }
                    }
                }
                else if (flag.HasFlag(SessionFlags.yellow))
                {
                    for (int i = 0; i < RGBDevices.Length; i++)
                    {
                        for (int j = 0; j < RGBDevices[i].Modes.Length; j++)
                        {
                            var mode = RGBDevices[i].Modes[j];
                            var len = (int)mode.ColorMax;
                            RGBClient.SetMode(i, 2, speed: null, direction: null, colors: null);
                            RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, len).Select(_ => new Color(255, 215, 0)).ToArray());

                        }
                    }
                }
                else if (flag.HasFlag(SessionFlags.yellowWaving))
                {
                    for (int i = 0; i < RGBDevices.Length; i++)
                    {
                        for (int j = 0; j < RGBDevices[i].Modes.Length; j++)
                        {
                            var mode = RGBDevices[i].Modes[j];
                            var len = (int)mode.ColorMax;
                            RGBClient.SetMode(i,4, speed: null, direction: null, colors: null);
                            RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, len).Select(_ => new Color(255, 215, 0)).ToArray());
                        }
                    }
                }
                else if (flag.HasFlag(SessionFlags.white))
                {
                    for (int i = 0; i < RGBDevices.Length; i++)
                    {
                        for (int j = 0; j < RGBDevices[i].Modes.Length; j++)
                        {
                            var mode = RGBDevices[i].Modes[j];
                            var len = (int)mode.ColorMax;
                            RGBClient.SetMode(i, 2, speed: null, direction: null, colors: null);
                            RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, len).Select(_ => new Color(255, 255, 255)).ToArray());
                        }
                    }
                }
                else if (flag.HasFlag(SessionFlags.startGo))
                {
                    for (int i = 0; i < RGBDevices.Length; i++)
                    {
                        for (int j = 0; j < RGBDevices[i].Modes.Length; j++)
                        {
                            var mode = RGBDevices[i].Modes[j];
                            var len = (int)mode.ColorMax;
                            RGBClient.SetMode(i, 2, speed: null, direction: null, colors: null);
                            //RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, len).Select(_ => new Color(0, 255, 0)).ToArray());
                            RGBClient.UpdateLeds(i, new Color[]
{
                                new(0, 255, 0)
});
                        }
                    }
                }
                else if (flag.HasFlag(SessionFlags.repair))
                {
                    for (int i = 0; i < RGBDevices.Length; i++)
                    {
                        for (int j = 0; j < RGBDevices[i].Modes.Length; j++)
                        {
                            var mode = RGBDevices[i].Modes[j];
                            var len = (int)mode.ColorMax;
                            //RGBClient.SetMode(i, 0);
                            //RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, len).Select(_ => new Color(255, 0, 0)).ToArray());
                            //                            RGBClient.UpdateLeds(i, new Color[]
                            //{
                            //                                new(255, 0, 0)
                            //});
                            RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, RGBDevices[i].Colors.Length).Select(_ => new Color(0, 255, 0)).ToArray());

                        }
                    }
                }
                else
                {

                    for (int i = 0; i < RGBDevices.Length; i++)
                    {
                        RGBClient.SetMode(i, 0);
                        RGBClient.UpdateLeds(i, colors: Enumerable.Range(0, RGBDevices[i].Colors.Length).Select(_ => new Color(255, 0, 0)).ToArray());
                    }
                }
            }
        }
}
}
