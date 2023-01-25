using iRacingSDK;
using OpenRGB.NET;
using OpenRGB.NET.Models;
using System;
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

                    Console.WriteLine(data.Telemetry.ToString());
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
                switch (flag)
                {
                    case SessionFlags.black:
                        device.Update(new Color[]
                        {
                                new(0, 0, 0)
                        });
                        break;
                    case SessionFlags.blue:
                        device.Update(new Color[]
                        {
                                new(0, 0, 255)
                        });
                        break;
                    case SessionFlags.green:
                        device.Update(new Color[]
                        {
                                new(0, 255, 0)
                        });
                        break;
                    case SessionFlags.caution:
                        device.Update(new Color[]
                        {
                                new(255, 255, 0)
                        });
                        break;
                    case SessionFlags.yellow:
                        device.Update(new Color[]
                        {
                                new(255, 215, 0)
                        });
                        break;
                    case SessionFlags.white:
                        device.Update(new Color[]
                        {
                                new(255,255,255)
                        });
                        break;
                    case SessionFlags.startGo:
                        device.Update(new Color[]
                        {
                                new(0, 139, 0)
                        });
                        break;
                    default:
                        device.Update(new Color[]
                        {
                                new(0, 255, 0)
                        });
                        break;
                }
            }
        }
    }
}
