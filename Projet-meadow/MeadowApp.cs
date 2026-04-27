using Meadow.Hardware;
using Meadow.Foundation.Leds;
using System;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using Meadow;
using Meadow.Devices;
using Meadow.Foundation.Sensors.Atmospheric;
using Meadow.Peripherals.Leds;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Newtonsoft.Json;

namespace Projet_meadow;

public class MeadowApp : App<F7FeatherV2>
{
    private RgbPwmLed _onboardLed;
    private Bmp280 bmp280;
    private IMqttClient mqttClient;

    const string iotHubHostName = "meadow-iot-hub.azure-devices.net";
    const string deviceId = "meadow-device";
    
    const string sasToken =
        "SharedAccessSignature sr=meadow-iot-hub.azure-devices.net%2Fdevices%2Fmeadow-device&sig=vDevnardEhgQNAzilq801IOmGtf8mS4XKUeEYfj9DS0%3D&se=1777286954";

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

        Resolver.Log.Info("Initializing MQTT client for Azure IoT Hub");

        var factory = new MqttFactory();
        mqttClient = factory.CreateMqttClient();

        var username = $"{iotHubHostName}/{deviceId}/?api-version=2021-04-12";

        var options = new MqttClientOptionsBuilder()
            .WithClientId(deviceId)
            .WithTcpServer(iotHubHostName, 8883)
            .WithCredentials(username, sasToken)
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311)
            .WithTls(new MqttClientOptionsBuilderTlsParameters
            {
                UseTls = true,
                SslProtocol = SslProtocols.Tls12
            })
            .Build();

        Resolver.Log.Info("Connecting to Azure IoT Hub via MQTT...");
        await mqttClient.ConnectAsync(options);
        Resolver.Log.Info("Connected to Azure IoT Hub.");

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
                messageId,
                deviceId,
                temperature,
                pressure,
                timestamp = DateTime.UtcNow
            };

            string messageString = JsonConvert.SerializeObject(telemetryDataPoint);

            var topic = $"devices/{deviceId}/messages/events/temperature-warning={(temperature > 30 ? "true" : "false")}";

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(messageString)
                .Build();

            Resolver.Log.Info($"Sending message {messageId} to Azure...");
            await mqttClient.PublishAsync(message);

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