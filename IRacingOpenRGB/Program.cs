using IRacingOpenRGB;
using iRacingSDK;
using OpenRGB.NET.Models;
using System;
using System.Linq;

try
{
    using var iRacingRGBWrapper = new ConnectionClientWrapper(); // Uses default OnNewSessionData from ConnectionClientWrapper, for changes has to be changed there
    //iRacingRGBWrapper.CheckConnections();
    iRacingRGBWrapper.StartTelemetryLoop();    
    Console.ReadLine();
    iRacingRGBWrapper.StopTelemetryLoop();
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine("Confirm to close window...");
    Console.ReadLine();
}

// Different approach to use a delegate instead of the default implementation for new session data
// try
// {
//     using var iRacingRGBWrapper = new ConnectionClientWrapper();
//     const string message = "This is a different message than in the connection client wrapper implementation, it's special, really, look how fancy!";
//     
//     void OnNewSessionData(DataSample dataSample)
//     {
//         try
//         {
//             var speed = dataSample.Telemetry.Speed;
//             foreach (var device in iRacingRGBWrapper.RGBDevices)
//             {
//                 foreach (var zone in device.Zones)
//                 {
//                     zone.Update(Color.GetHueRainbow((int)zone.LedCount).ToArray());
//                 }
//             }
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine(ex.Message);
//             Console.WriteLine(ex.StackTrace);
//         }
//         Console.WriteLine(message);
//     }
//
//     iRacingRGBWrapper.IRacingNewSessionDataHandler = OnNewSessionData;
//     
//     iRacingRGBWrapper.CheckConnections();
//     Console.ReadLine();
//
// }
// catch (Exception ex)
// {
//     Console.WriteLine(ex.Message);
//     Console.WriteLine(ex.StackTrace);
//     Console.WriteLine("Confirm to close window...");
//     Console.ReadLine();
// }
