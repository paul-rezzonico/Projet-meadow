using Meadow.Hardware;
using Meadow.Foundation.Leds;
using System;
using System.Threading.Tasks;
using Meadow;
using Meadow.Devices;
using Meadow.Peripherals.Leds;

namespace Projet_meadow;

public class MeadowApp : App<F7FeatherV2>
{
    RgbPwmLed onboardLed;

    public override async Task Initialize()
    {
        Resolver.Log.Info("Initialize...");

        onboardLed = new RgbPwmLed(
            redPwmPin: Device.Pins.OnboardLedRed,
            greenPwmPin: Device.Pins.OnboardLedGreen,
            bluePwmPin: Device.Pins.OnboardLedBlue,
            CommonType.CommonAnode);

        onboardLed.StartPulse(Color.Red);

        Resolver.Log.Info("Waiting wifi to be up");

        var wifi = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();

        while (!wifi.IsConnected)
        {
            await Task.Delay(500);
        }

        onboardLed.SetColor(Color.Green);

        await base.Initialize();
    }

    public override Task Run()
    {
        Resolver.Log.Info("Run...");
        return Task.CompletedTask;
    }
}