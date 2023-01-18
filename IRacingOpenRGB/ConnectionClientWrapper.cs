using iRacingSDK;
using OpenRGB.NET;
using OpenRGB.NET.Models;
using System;

namespace IRacingOpenRGB
{
    public class ConnectionClientWrapper : IDisposable
    {
        public const string DefaultRGBClientName = "IRacing RGB Client";
        public iRacingConnection RacingConnection { get; init; }
        public Action<DataSample> IRacingNewSessionDataHandler
        {
            get { return iRacingNewSessionDataHandler;}
            set
            {
                if (RacingConnection != null)
                {
                    if (IRacingNewSessionDataHandler != null)
                        RacingConnection.NewSessionData -= iRacingNewSessionDataHandler;

                    if (value != null)
                        RacingConnection.NewSessionData += value;
                }

                iRacingNewSessionDataHandler = value;
            }
        }
        private Action<DataSample> iRacingNewSessionDataHandler;

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

        public void OnNewSessionData(DataSample dataSample)
        {
            try
            {
                var speed = dataSample.Telemetry.Speed;
                foreach (var device in RGBDevices)
                {
                    switch (speed)
                    {
                        case > 200:
                            device.Update(new Color[]
                            {
                                new(255, 0, 0)
                            });
                            break;
                        case > 100:
                            device.Update(new Color[]
                            {
                                new(0, 0, 255)
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
