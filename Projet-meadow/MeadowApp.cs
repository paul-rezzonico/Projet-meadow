using Amqp;
using Amqp.Framing;
using Meadow;
using Meadow.Devices;
using Meadow.Foundation.Leds;
using Meadow.Foundation.Sensors.Atmospheric;
using Meadow.Hardware;
using Meadow.Peripherals.Leds;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Meadow.Logging;

namespace Projet_meadow;

public class MeadowApp : App<F7FeatherV2>
{
    private RgbPwmLed _onboardLed;
    private Bmp280 bmp280;
    private CloudLogger cloudLogger;

    private Connection connection;
    private SenderLink sender;

    private const string HubName = "YOUR_IOT_HUB_NAME";
    private const string DeviceId = "YOUR_DEVICE_ID";

    private const string SasToken =
        "YOUR_SAS_TOKEN";
    public override async Task Initialize()
    {
        Resolver.Log.Info("Initialize...");

        _onboardLed = new RgbPwmLed(
            redPwmPin: Device.Pins.OnboardLedRed,
            greenPwmPin: Device.Pins.OnboardLedGreen,
            bluePwmPin: Device.Pins.OnboardLedBlue,
            CommonType.CommonAnode);

        // turn on the onboard LED while initializing
        await _onboardLed.StartPulse(Color.Red);
        
        Resolver.Log.Info("Initializing Meadow.Cloud logger...");
        cloudLogger = new CloudLogger();

        // Add the CloudLogger to the Resolver's Log and Services.'
        Resolver.Log.AddProvider(cloudLogger);
        Resolver.Services.Add(cloudLogger);

        await WaitForWifiReady();

        _onboardLed.SetColor(Color.Green);
        Resolver.Log.Info("WiFi is ready!");

        Resolver.Log.Info("Initializing I2C Bus...");
        var i2cBus = Device.CreateI2cBus();

        Resolver.Log.Info("Initializing BMP280 sensor...");
        bmp280 = new Bmp280(i2cBus);

        Resolver.Log.Info("Initializing Azure IoT Hub AMQP...");
        await InitializeAzureAmqp();
        Resolver.Log.Info("Initialize completed.");

        await base.Initialize();
    }

    public override async Task Run()
    {
        Resolver.Log.Info("Run...");

        int messageId = 0;

        while (true)
        {
            try
            {
                // read the sensor
                var result = await bmp280.Read();

                double temperature = result.Temperature.Value.Celsius;
                double pressure = result.Pressure.Value.Hectopascal;

                Resolver.Log.Info($"Current temperature: {temperature:0.00} °C");
                Resolver.Log.Info($"Current pressure: {pressure:0.00} hPa");

                // send the data to Azure IoT Hub
                await SendDataToAzureAmqp(temperature, pressure, messageId++);
                
                // send the data to Meadow.Cloud
                SendDataToMeadowCloud(temperature, pressure, messageId++);

                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                Resolver.Log.Error($"Error in Run loop: {ex.GetType().FullName}");
                Resolver.Log.Error(ex.Message);

                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
    }

    private async Task WaitForWifiReady()
    {
        Resolver.Log.Info("Waiting WiFi to be ready...");

        var wifi = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();

        int retry = 0;

        // Wait for WiFi to be ready.
        while (!wifi.IsConnected)
        {
            Resolver.Log.Info($"WiFi not connected yet... retry {retry++}");
            await Task.Delay(1000);
        }

        Resolver.Log.Info("WiFi connected.");
        Resolver.Log.Info($"IP Address: {wifi.IpAddress}");

        // Wait additional time for the network stack to be fully ready before attempting to connect to Azure IoT Hub.
        Resolver.Log.Info("Waiting for network stack to be ready...");
        await Task.Delay(30000);
    }

    private async Task InitializeAzureAmqp()
    {
        try
        {
            string hostName = HubName + ".azure-devices.net";
            string userName = DeviceId + "@sas." + HubName;
            string senderAddress = "devices/" + DeviceId + "/messages/events";

            Resolver.Log.Info("Create AMQP connection factory...");
            var factory = new ConnectionFactory();

            Resolver.Log.Info("Create AMQP connection...");
            connection = await factory.CreateAsync(
                new Address(hostName, 5671, userName, SasToken)
            );

            Resolver.Log.Info("Create AMQP session...");
            var session = new Session(connection);

            Resolver.Log.Info("Create AMQP SenderLink...");
            sender = new SenderLink(session, "send-link", senderAddress);

            Resolver.Log.Info("Azure IoT Hub AMQP initialized.");
        }
        catch (Exception ex)
        {
            Resolver.Log.Error($"AMQP initialization failed: {ex.GetType().FullName}");
            Resolver.Log.Error(ex.Message);

            _onboardLed?.SetColor(Color.Red);
        }
    }

    private async Task SendDataToAzureAmqp(double temperature, double pressure, int messageId)
    {
        if (sender == null)
        {
            Resolver.Log.Error("AMQP sender is null. Message not sent.");
            return;
        }

        try
        {
            string messagePayload =
                "{"
                + $"\"messageId\":{messageId},"
                + $"\"deviceId\":\"{DeviceId}\","
                + $"\"temperature\":{temperature},"
                + $"\"pressure\":{pressure},"
                + $"\"timestamp\":\"{DateTime.UtcNow:O}\""
                + "}";

            Resolver.Log.Info("Create AMQP message payload");
            Resolver.Log.Info(messagePayload);

            byte[] payloadBytes = Encoding.UTF8.GetBytes(messagePayload);

            var message = new Message
            {
                BodySection = new Data
                {
                    Binary = payloadBytes
                }
            };

            message.ApplicationProperties = new ApplicationProperties();
            message.ApplicationProperties["temperature-warning"] =
                temperature > 30 ? "true" : "false";

            Resolver.Log.Info($"Sending AMQP message {messageId}...");
            await sender.SendAsync(message);

            Resolver.Log.Info(
                $"*** AMQP - DATA SENT - Temperature: {temperature:0.00} °C, Pressure: {pressure:0.00} hPa ***"
            );
        }
        catch (Exception ex)
        {
            Resolver.Log.Error($"AMQP send failed: {ex.GetType().FullName}");
            Resolver.Log.Error(ex.Message);
        }
    }
    
    private void SendDataToMeadowCloud(double temperature, double pressure, int messageId)
    {
        var wifi = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();
        if (cloudLogger == null)
        {
            Resolver.Log.Error("CloudLogger is null. Data not sent.");
            return;
        }

        if (wifi == null || !wifi.IsConnected)
        {
            Resolver.Log.Error("WiFi is not connected. Data not sent to Meadow.Cloud.");
            return;
        }

        Resolver.Log.Info($"Sending event {messageId} to Meadow.Cloud...");

        cloudLogger.LogEvent(
            eventId: 1000,
            description: "bmp280 reading",
            measurements: new Dictionary<string, object>
            {
                { "messageId", messageId.ToString() },
                { "temperature", temperature.ToString("N2") },
                { "pressure", pressure.ToString("N2") },
                { "timestamp", DateTime.UtcNow.ToString("O") }
            }
        );

        Resolver.Log.Info("Data sent to Meadow.Cloud.");
    }
}
