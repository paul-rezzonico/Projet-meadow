# Projet-meadow

This project is a Meadow F7 Feather application that reads atmospheric data (temperature and pressure) from a BMP280 sensor and sends it to:
1. **Azure IoT Hub** via AMQP.
2. **Meadow.Cloud** using the built-in CloudLogger.

## Prerequisites

- [Meadow F7 Feather](https://store.wildernesslabs.co/collections/frontpage/products/meadow-f7-feather) (v2 recommended).
- [BMP280 Sensor](https://www.bosch-sensortec.com/products/environmental-sensors/pressure-sensors/bmp280/) connected via I2C.
- [Meadow CLI](https://developer.wildernesslabs.co/Meadow/Meadow_Tools/Meadow_CLI/) installed.
- [Azure IoT Hub](https://azure.microsoft.com/en-us/products/iot-hub/) instance.

## Getting Started

### 1. Meadow F7 Feather Setup
Follow the official Wilderness Labs guide to set up your Meadow F7 Feather:
[Getting Started with Meadow F7 Feather](https://developer.wildernesslabs.co/Meadow/Getting_Started/MCUs/F7_Feather/)

### 2. Register Your Device
To use Meadow.Cloud features, you must register your device using the Meadow CLI:
```bash
meadow device provision
```

### 3. Azure IoT Hub Key (SAS Token)
You need to generate a Shared Access Signature (SAS) token for your device to authenticate with Azure IoT Hub.
- Generate the token using the Azure CLI or Azure IoT Explorer.
- Update the `SasToken` constant in `Projet-meadow/MeadowApp.cs`:
```csharp
private const string SasToken = "SharedAccessSignature sr=...";
```

## Configuration

### WiFi
Update `Projet-meadow/wifi.config.yaml` with your network credentials:
```yaml
Credentials:
  SSID: YOUR_SSID
  Password: YOUR_PASSWORD
```

### App Settings
Check `Projet-meadow/app.config.yaml` and `Projet-meadow/meadow.config.yaml` for other application and device settings.

## Hardware Wiring (I2C)

Connect the BMP280 sensor to the Meadow F7 Feather:
- **VCC** -> 3.3V
- **GND** -> GND
- **SCL** -> SCL (D08)
- **SDA** -> SDA (D07)

## Deployment

1. Connect your Meadow F7 Feather to your computer via USB.
2. Open the solution in **Rider** or **Visual Studio 2022**.
3. Build the project.
4. Deploy to the device.

## Troubleshooting

- **WiFi Issues:** Ensure your SSID and Password are correct in `wifi.config.yaml`. The onboard LED will pulse **Red** during initialization and turn **Green** once WiFi is connected.
- **AMQP Errors:** Verify your `HubName`, `DeviceId`, and `SasToken` in `MeadowApp.cs`.
- **Sensor Errors:** Check your I2C wiring.
