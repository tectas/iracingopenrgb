using iRacingSDK;
using OpenRGB.NET;
using OpenRGB.NET.Models;

using var client = new OpenRGBClient(name: "IRacing RGB Client", autoconnect: true, timeout: 1000);
var iRacingCon = new iRacingConnection();

var devices = client.GetAllControllerData();

void RacingConOnNewSessionData(DataSample dataSample)
{
    var speed = dataSample.Telemetry.Speed;
    foreach (var device in devices)
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

iRacingCon.NewSessionData += RacingConOnNewSessionData;
