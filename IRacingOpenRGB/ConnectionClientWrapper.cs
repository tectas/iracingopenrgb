using iRacingSDK;
using Newtonsoft.Json;
using OpenRGB.NET;
using OpenRGB.NET.Enums;
using OpenRGB.NET.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IRacingOpenRGB
{
    public class ConcurrentBagJsonConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ConcurrentBag<T>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (objectType == typeof(ConcurrentBag<T>))
            {
                var elements = new ConcurrentBag<T>();

                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                    }
                    else
                        elements.Add(serializer.Deserialize<T>(reader));
                }
                return elements;
            }

            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is ConcurrentBag<T> listVal)
            {
                writer.WriteStartObject();
                foreach (var item in listVal)
                {
                    serializer.Serialize(writer, item);
                }
                writer.WriteEndObject();
            }
        }
    }

    public class TelemetryData
    {

        public ConcurrentBag<float> FuelUsePerHour
        {
            get { return _fuelUsePerHour; }
            set { FuelUsePerHour = _fuelUsePerHour; }
        }
        private readonly ConcurrentBag<float> _fuelUsePerHour = new();
    }

    public class ConnectionClientWrapper : IDisposable
    {
        public const string DefaultRGBClientName = "IRacing RGB Client";
        public const string DefaultExportJSONFileName = "telemetry_export.json";
        public const int ExportInterval = 5 * 60 * 1000;

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

        private Task _rgbTelemetryLoopTask;
        private Task _fuelTelemetryLoopTask;
        private Task _exportTask;

        private CancellationTokenSource _rgbCancellationTokenSource;

        private CancellationTokenSource _fuelCancellationTokenSource;
        private CancellationTokenSource _exportCancellationTokenSource;

        public string RGBClientName { get; set; }
        public OpenRGBClient RGBClient { get; init; }

        public Device[] RGBDevices { get; init; }

        public TelemetryData TelemetryData { get; set; }

        private bool _disposed = false;

        public ConnectionClientWrapper(string rgbClientName = DefaultRGBClientName)
        {
            RacingConnection = new iRacingConnection();

            IRacingNewSessionDataHandler = OnNewSessionData;

            //RGBClientName = rgbClientName;
            //RGBClient = new OpenRGBClient(name: rgbClientName, autoconnect: true, timeout: 1000);

            //RGBDevices = RGBClient.GetAllControllerData();

            TelemetryData = new TelemetryData();
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
                _rgbTelemetryLoopTask?.Dispose();

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

        public void StartRGBTelemetryLoop()
        {
            if (_rgbTelemetryLoopTask is { IsCanceled: false })
                return;

            var cancellationTokenSource = new CancellationTokenSource();

            _rgbTelemetryLoopTask = Task.Factory.StartNew(() =>
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

            _rgbCancellationTokenSource = cancellationTokenSource;
        }

        public void StopRGBTelemetryLoop()
        {
            _rgbCancellationTokenSource?.Cancel();
            _rgbTelemetryLoopTask = null;

            Console.WriteLine("Telemetry loop stopped");
        }

        public void StartFuelTelemetryLoop()
        {
            if (_fuelTelemetryLoopTask is { IsCanceled: false })
                return;

            var cancellationTokenSource = new CancellationTokenSource();

            _fuelTelemetryLoopTask = Task.Factory.StartNew(() =>
            {
                if (cancellationTokenSource.IsCancellationRequested)
                    return;

                Console.WriteLine("Fuel Telemetry loop started");

                foreach (var data in RacingConnection.GetDataFeed()
                    .WithCorrectedPercentages()
                    .WithCorrectedDistances()
                    .WithPitStopCounts())
                {
                    if (cancellationTokenSource.IsCancellationRequested)
                        return;

                    TelemetryData.FuelUsePerHour.Add(data.Telemetry.FuelUsePerHour);
                }
            }, cancellationTokenSource.Token);

            _fuelCancellationTokenSource = cancellationTokenSource;
        }

        public void StopFuelTelemetryLoop()
        {
            _fuelCancellationTokenSource?.Cancel();
            _fuelTelemetryLoopTask = null;

            Console.WriteLine(" Fuel Telemetry loop stopped");
        }

        public void StartExportWorker()
        {
            if (_exportTask is { IsCanceled: false })
                return;

            var cancellationTokenSource = new CancellationTokenSource();
            
            Console.WriteLine("Export loop started");
            Console.WriteLine($"First export in {ExportInterval / 1000 / 60} minutes");
            
            _exportTask = Task.Factory.StartNew(() =>
            {
                cancellationTokenSource.Token.WaitHandle.WaitOne(ExportInterval);

                using StreamWriter file = File.CreateText(DefaultExportJSONFileName);

                if (cancellationTokenSource.IsCancellationRequested)
                    return;
                
                JsonSerializer serializer = new JsonSerializer
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };
                serializer.Converters.Add(new ConcurrentBagJsonConverter<float>());
                serializer.Serialize(file, TelemetryData);

                Console.WriteLine($"File exported: {DateTime.Now.ToString()}");
            }, cancellationTokenSource.Token);

            _exportCancellationTokenSource = cancellationTokenSource;
        }

        public void StopExportWorker()
        {
            _exportCancellationTokenSource?.Cancel();
            _exportCancellationTokenSource = null;

            Console.WriteLine("Export loop stopped");
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
                            RGBClient.SetMode(i, 4, speed: null, direction: null, colors: null);
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
