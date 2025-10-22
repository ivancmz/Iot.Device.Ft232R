# UART and GPIO driver for FT232R

This project supports UART and GPIO in Windows (32 or 64 bits), Linux and MacOS environments through the FT232R chipset.

## Documentation

The product datasheets and application notes can be found here:

- [FT232R Datasheet (DS_FT232R.pdf)](https://www.ftdichip.com/Support/Documents/DataSheets/ICs/DS_FT232R.pdf)
- [D2XX Programmer's Guide](https://www.ftdichip.com/Support/Documents/ProgramGuides/D2XX_Programmer's_Guide(FT_000071).pdf)
- [AN232R-01: Bit Bang Modes for the FT232R](https://www.ftdichip.com/Support/Documents/AppNotes/AN_232R-01_Bit_Bang_Mode_Available_For_FT232R_and_Vinculum_VNC1L.pdf)

You can find this chipset on many USB-to-Serial adapter boards, such as:
- [Adafruit FTDI Friend](https://www.adafruit.com/product/284)
- [SparkFun FTDI Basic Breakout](https://www.sparkfun.com/products/9716)
- Generic USB-to-Serial cables and adapters

[FTDI](https://www.ftdichip.com/) has multiple chips that may look similar. You will find other implementations with the [FT232H](../Ft232H/README.md) and [FT4222](../Ft4222/README.md).

## Key Differences: FT232R vs FT232H

| Feature | FT232H | FT232R |
|---------|---------|---------|
| MPSSE | ✓ Supported | ✗ NOT supported |
| I2C | ✓ Via MPSSE | ✗ NOT available |
| SPI | ✓ Via MPSSE | ✗ NOT available |
| UART | ✓ | ✓ |
| GPIO Pins | 16 pins (ADBUS0-7, ACBUS0-7) | **4 CBUS pins (CBUS0-3)** |
| BitBang Mode | Asynchronous/Synchronous | **CBUS Bit Bang Mode (0x20)** |
| Simultaneous UART + GPIO | Limited | **✓ Yes (4 GPIO pins)** |

## Requirements

Once plugged into your Windows, MacOS or Linux machine, the [D2xx drivers](https://ftdichip.com/drivers/d2xx-drivers/) must be properly installed. This implementation uses this driver directly.

Unlike the FT232H, the FT232R does **not** support the Multi-Protocol Synchronous Serial Engine (MPSSE), so it cannot be used for SPI or I2C protocols. However, it excels at UART communication and provides 4 GPIO pins (CBUS0-3) that can be used simultaneously with UART via **CBUS Bit Bang Mode**.

### Important Note on CBUS4 (PWREN#)

The FT232R has a 5th CBUS pin (CBUS4), but it is **recommended to configure it as PWREN#** for bus-powered designs, as specified in the datasheet. This allows proper power management for USB bus-powered applications. Therefore, this driver only exposes **4 GPIO pins (CBUS0-3)** for general use, allowing CBUS4 to be reserved for power control.

If you need to use CBUS4 as GPIO, you must ensure your design is not relying on the PWREN# functionality and reconfigure the EEPROM accordingly using FTDI's FT_PROG utility.

## CBUS Bit Bang Mode

The FT232R uses a special **CBUS Bit Bang Mode (0x20)** for GPIO operations. This mode uses a single byte with the format **MMMMVVVV**:

- **Upper nibble (MMMM)**: Direction mask (0=Input, 1=Output)
- **Lower nibble (VVVV)**: Output value mask (0=Low, 1=High)

For example, to set CBUS0 as output high and CBUS1 as input:
```
MMMM = 0001 (only CBUS0 is output)
VVVV = 0001 (CBUS0 value is high)
Result: 0x11
```

This allows simultaneous use of UART and 4 GPIO pins, making the FT232R ideal for applications requiring serial communication with additional control signals.

## Usage

### Getting Device List

You can get the list of FTDI devices like this:

```csharp
using Iot.Device.FtCommon;
using Iot.Device.Ft232R;

var devices = FtCommon.GetDevices();
Console.WriteLine($"{devices.Count} available device(s)");
foreach (var device in devices)
{
    Console.WriteLine($"  {device.Description}");
    Console.WriteLine($"    Flags: {device.Flags}");
    Console.WriteLine($"    Id: {device.Id}");
    Console.WriteLine($"    LocId: {device.LocId}");
    Console.WriteLine($"    Serial number: {device.SerialNumber}");
    Console.WriteLine($"    Type: {device.Type}");
}
```

You can also filter only FT232R devices:

```csharp
var ft232rDevices = Ft232RDevice.GetFT232R();
if (ft232rDevices.Count == 0)
{
    Console.WriteLine("No FT232R device found");
    return;
}

var ft232r = ft232rDevices[0];
```

### Easy Pin Numbering

The FT232R has 4 CBUS pins that can be referenced by name:

```csharp
int pin0 = Ft232RDevice.GetPinNumberFromString("CBUS0"); // or "CB0"
int pin1 = Ft232RDevice.GetPinNumberFromString("CBUS1"); // or "CB1"
int pin2 = Ft232RDevice.GetPinNumberFromString("CBUS2"); // or "CB2"
int pin3 = Ft232RDevice.GetPinNumberFromString("CBUS3"); // or "CB3"
```

### Creating a UART Device

You can create a UART device with customizable settings:

```csharp
using System.Device.Uart;
using Iot.Device.Ft232R;

var ft232r = Ft232RDevice.GetFT232R()[0];

var settings = new UartConnectionSettings("FT232R")
{
    BaudRate = 115200,
    DataBits = 8,
    Parity = UartParity.None,
    StopBits = UartStopBits.One,
    FlowControl = UartFlowControl.None,
    ReadTimeout = 1000,
    WriteTimeout = 1000
};

using (var uart = ft232r.CreateUartDevice(settings))
{
    // Write data
    string message = "Hello UART!";
    byte[] writeBuffer = Encoding.ASCII.GetBytes(message);
    uart.Write(writeBuffer);

    // Read data
    byte[] readBuffer = new byte[100];
    int bytesRead = uart.Read(readBuffer);

    if (bytesRead > 0)
    {
        string received = Encoding.ASCII.GetString(readBuffer, 0, bytesRead);
        Console.WriteLine($"Received: {received}");
    }
}
```

### Creating a GPIO Controller

You can create a GPIO controller to control the 4 CBUS pins:

```csharp
using System.Device.Gpio;
using Iot.Device.Ft232R;

var ft232r = Ft232RDevice.GetFT232R()[0];
var gpioController = ft232r.CreateGpioController();

// Use CBUS0 as output
int ledPin = Ft232RDevice.GetPinNumberFromString("CBUS0");
gpioController.OpenPin(ledPin);
gpioController.SetPinMode(ledPin, PinMode.Output);

// Blink LED
while (true)
{
    gpioController.Write(ledPin, PinValue.High);
    Thread.Sleep(500);
    gpioController.Write(ledPin, PinValue.Low);
    Thread.Sleep(500);
}
```

### Using UART and GPIO Simultaneously

One of the key advantages of the FT232R is the ability to use UART and GPIO at the same time:

```csharp
using System.Device.Gpio;
using System.Device.Uart;
using Iot.Device.Ft232R;

var ft232r = Ft232RDevice.GetFT232R()[0];

// Create UART
var uartSettings = new UartConnectionSettings("FT232R")
{
    BaudRate = 9600,
    DataBits = 8,
    Parity = UartParity.None,
    StopBits = UartStopBits.Two
};

using (var uart = ft232r.CreateUartDevice(uartSettings))
{
    // Create GPIO controller
    var gpioController = ft232r.CreateGpioController();

    // Setup CBUS0 as output (LED or status indicator)
    int statusPin = 0;
    gpioController.OpenPin(statusPin);
    gpioController.SetPinMode(statusPin, PinMode.Output);

    Console.WriteLine("UART + GPIO active");
    Console.WriteLine("Status LED will blink while echoing UART data");

    int counter = 0;
    while (true)
    {
        // Blink status LED
        gpioController.Write(statusPin, counter++ % 2 == 0 ? PinValue.High : PinValue.Low);

        // Check for UART data and echo it back
        if (uart.BytesToRead > 0)
        {
            byte[] buffer = new byte[uart.BytesToRead];
            int bytesRead = uart.Read(buffer);

            if (bytesRead > 0)
            {
                uart.Write(buffer.AsSpan(0, bytesRead));
                Console.WriteLine($"Echoed {bytesRead} bytes");
            }
        }

        Thread.Sleep(200);
    }
}
```

## Important Notes

### GPIO Limitations

- The FT232R provides **only 4 GPIO pins (CBUS0-3)** available for general use
- CBUS4 should be configured as **PWREN#** for bus-powered designs (recommended)
- All 4 GPIO pins can be used simultaneously with UART communication
- GPIO uses **CBUS Bit Bang Mode (0x20)**, not the standard asynchronous bit bang mode

### UART Configuration

- Baud rates: 300 to 3,000,000 baud (3 Mbaud)
- Data bits: 7 or 8
- Stop bits: 1, 1.5, or 2
- Parity: None, Odd, Even, Mark, Space
- Flow control: None, RTS/CTS, XON/XOFF

### Pin Availability

When using UART, the TX and RX pins are dedicated to serial communication. The 4 CBUS pins remain available for GPIO operations.

## Project Status and Future Plans

This project has been designed with the intention of contributing it to the official [.NET IoT repository](https://github.com/dotnet/iot/tree/main). For this reason:

- We use the same namespaces as the official repository (`Iot.Device.Ft232R`, `Iot.Device.FtCommon`, `System.Device.Uart`)
- We follow the same coding patterns and conventions used in the FT232H and FT4222 drivers
- Some files from `Iot.Device.FtCommon` have been duplicated in this project because they are marked as `internal` in the original NuGet package and are not accessible externally

Once the driver has been thoroughly tested and validated, we plan to:

1. Fork the [dotnet/iot](https://github.com/dotnet/iot) repository
2. Integrate this FT232R driver into the official codebase structure
3. Submit a pull request for inclusion in the official repository

This will allow the broader .NET IoT community to benefit from FT232R support alongside the existing FT232H and FT4222 drivers.

## Sample Code

The `samples` directory contains a complete demonstration program that shows:

1. **GPIO Test**: Blink all 4 CBUS pins sequentially, then read their states
2. **UART Test**: Loopback test (requires TX and RX to be connected)
3. **Combined Test**: Simultaneous UART echo with status LED blinking on GPIO

To run the sample:

```bash
cd samples
dotnet run
```

## References

- [FT232R Datasheet](https://www.ftdichip.com/Support/Documents/DataSheets/ICs/DS_FT232R.pdf)
- [D2XX Programmer's Guide](https://www.ftdichip.com/Support/Documents/ProgramGuides/D2XX_Programmer's_Guide(FT_000071).pdf)
- [FTDI Chip Website](https://www.ftdichip.com/)
- [.NET IoT Repository](https://github.com/dotnet/iot)
