using Meadow.Hardware;
using Meadow.Foundation.Leds;
using System;
using System.Threading.Tasks;
using Meadow;
using Meadow.Devices;
using Meadow.Foundation.Sensors.Atmospheric;
using Meadow.Peripherals.Leds;

namespace Projet_meadow;

public class MeadowApp : App<F7FeatherV2>
{
    private RgbPwmLed _onboardLed;
    private Bmp280 bmp280;

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

        await base.Initialize();
    }

    public override Task Run()
    {
        Resolver.Log.Info("Run...");
        Resolver.Log.Info($"Current temperature: {GetTemperature(bmp280):0.00} °C");
        Resolver.Log.Info($"Current pressure: {GetPressure(bmp280):0.00} hPa");
        System.Threading.Thread.Sleep(1000);
        return Task.CompletedTask;
    }
    
    private static double GetTemperature(Bmp280 bmp280)
    {
        var result = bmp280.Read();
        return result.Result.Temperature.Value.Celsius;
    }
    
    private static double GetPressure(Bmp280 bmp280)
    {
        var result = bmp280.Read();
        return result.Result.Pressure.Value.Hectopascal;
    }
}