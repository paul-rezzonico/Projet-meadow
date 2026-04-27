using Meadow.Hardware;
using Meadow.Foundation.Leds;
using System;
using System.Text;
using System.Threading.Tasks;
using Meadow;
using Meadow.Devices;
using Meadow.Foundation.Sensors.Atmospheric;
using Meadow.Peripherals.Leds;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;

namespace Projet_meadow;

public class MeadowApp : App<F7FeatherV2>
{
    private RgbPwmLed _onboardLed;
    private Bmp280 bmp280;
    private DeviceClient deviceClient;

    const string iotHubHostName = "meadow-iot-hub.azure-devices.net";
    const string deviceId = "meadow-device";
    const string deviceKey = "Uhl3sq6VVqwZhnL5VI1QBZmzFMra6rKz9v7N6de6R6s=";

    public override async Task Initialize()
    {
        Resolver.Log.Info("Initialize...");

        _onboardLed = new RgbPwmLed(
            redPwmPin: Device.Pins.OnboardLedRed,
            greenPwmPin: Device.Pins.OnboardLedGreen,
            bluePwmPin: Device.Pins.OnboardLedBlue,
            CommonType.CommonAnode);

        await _onboardLed.StartPulse(Color.Red);

        Resolver.Log.Info("Waiting wifi to be up");

        var wifi = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();

        while (!wifi.IsConnected)
        {
            await Task.Delay(500);
        }

        _onboardLed.SetColor(Color.Green);
        Resolver.Log.Info("Wifi is up!");

        Resolver.Log.Info("Initializing I2C Bus");
        var i2cBus = Device.CreateI2cBus();
        Resolver.Log.Info("Initializing Bmp280 sensor");
        bmp280 = new Bmp280(i2cBus);

        Resolver.Log.Info("Initializing Azure IoT Client");
        var deviceAuthentication = new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, deviceKey);
        deviceClient = DeviceClient.Create(iotHubHostName, deviceAuthentication, TransportType.Mqtt);

        await base.Initialize();
    }

    public override async Task Run()
    {
        Resolver.Log.Info("Run...");

        int messageId = 0;

        while (true)
        {
            var result = await bmp280.Read();
            double temp = result.Temperature.Value.Celsius;
            double pressure = result.Pressure.Value.Hectopascal;

            Resolver.Log.Info($"Current temperature: {temp:0.00} °C");
            Resolver.Log.Info($"Current pressure: {pressure:0.00} hPa");

            await SendDataToAzure(temp, pressure, messageId++);

            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    }

    private async Task<bool> SendDataToAzure(double temperature, double pressure, int messageId)
    {
        try
        {
            var telemetryDataPoint = new
            {
                messageId = messageId,
                deviceId = deviceId,
                temperature = temperature,
                pressure = pressure,
                timestamp = DateTime.UtcNow
            };

            string messageString = JsonConvert.SerializeObject(telemetryDataPoint);
            var message = new Message(Encoding.UTF8.GetBytes(messageString));
            
            message.Properties.Add("temperature-warning", (temperature > 30) ? "true" : "false");

            Resolver.Log.Info($"Sending message {messageId} to Azure...");
            await deviceClient.SendEventAsync(message);
            Resolver.Log.Info("Data sent successfully.");
            
            return true;
        }
        catch (Exception ex)
        {
            Resolver.Log.Error($"Error sending data to Azure: {ex.Message}");
            return false;
        }
    }
}