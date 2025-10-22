// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Device.Gpio;
using System.Device.Uart;
using System.Text;
using System.Threading;
using Iot.Device.Ft232R;
using Iot.Device.FtCommon;

Console.WriteLine("Hello FT232R");
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

if (devices.Count == 0)
{
    Console.WriteLine("No device connected");
    return;
}

var ft232rDevices = Ft232RDevice.GetFT232R();
if (ft232rDevices.Count == 0)
{
    Console.WriteLine("No FT232R device found");
    return;
}

Ft232RDevice ft232r = ft232rDevices[0];
// Optional, you can make sure the device is reset
ft232r.Reset();

Console.WriteLine("\n====== Select Test ======");
Console.WriteLine("1. GPIO Test (blink all CBUS pins)");
Console.WriteLine("2. UART Test (loopback - connect TX to RX)");
Console.WriteLine("3. Combined Test (UART + GPIO simultaneously)");
Console.Write("Enter choice (1-3): ");

var choice = Console.ReadLine();
switch (choice)
{
    case "1":
        TestGpio(ft232r);
        break;
    case "2":
        TestUart(ft232r);
        break;
    case "3":
        TestCombined(ft232r);
        break;
    default:
        Console.WriteLine("Invalid choice, running GPIO test...");
        TestGpio(ft232r);
        break;
}

void TestGpio(Ft232RDevice ft232r)
{
    var gpioController = ft232r.CreateGpioController();
    // FT232R has 4 CBUS pins (CBUS0-CBUS3)
    for (int i = 0; i < ft232r.PinCount; i++)
    {

        string PinName = "CBUS" + i;
        int pinNumber = Ft232RDevice.GetPinNumberFromString(PinName);

        if (pinNumber < 0)
        {
            Console.WriteLine($"Invalid pin name: {PinName}");
            return;
        }

        // Opening the pin
        gpioController.OpenPin(pinNumber);
        gpioController.SetPinMode(pinNumber, PinMode.Output);

        Console.WriteLine($"Blinking {PinName} (pin {pinNumber})");
        if(i < ft232r.PinCount - 1)
        {
            Console.WriteLine("Press any key to stop blinking and switch to the next pin...");
        }
        else
            Console.WriteLine("Press any key to stop blinking and switch to input mode...");

        while (!Console.KeyAvailable)
        {
            gpioController.Write(pinNumber, PinValue.High);
            Thread.Sleep(500);
            gpioController.Write(pinNumber, PinValue.Low);
            Thread.Sleep(500);
        }
        Console.ReadKey();
        Console.WriteLine($"\nClosing {PinName} pin...");
        gpioController.ClosePin(pinNumber);
    }

    for (int i = 0; i < ft232r.PinCount; i++)
    {
        string PinName = "CBUS" + i;
        int pinNumber = Ft232RDevice.GetPinNumberFromString(PinName);
        Console.WriteLine($"\nReading {PinName} state (press any key to exit)...");
        gpioController.OpenPin(pinNumber);
        gpioController.SetPinMode(pinNumber, PinMode.Input);

        while (!Console.KeyAvailable)
        {
            var state = gpioController.Read(pinNumber);
            Console.Write($"State: {state}    ");
            Console.CursorLeft = 0;
            Thread.Sleep(50);
        }
        Console.ReadKey();
        Console.WriteLine("\nClosing pin ...");
        gpioController.ClosePin(pinNumber);
    }
    Console.WriteLine("Exiting...");
}

void TestUart(Ft232RDevice ft232r)
{
    Console.WriteLine("\n====== UART Loopback Test ======");
    Console.WriteLine("Make sure TX and RX are connected together for loopback test");
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey();

    // Create UART device with specific settings
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
        Console.WriteLine($"UART opened: {settings.BaudRate} baud, {settings.DataBits}N{(int)settings.StopBits + 1}");

        while (!Console.KeyAvailable)
        {
            // Test data
            string testMessage = "Hello FT232R UART!";
            byte[] writeBuffer = Encoding.ASCII.GetBytes(testMessage);

            Console.WriteLine($"\nSending: \"{testMessage}\"");
            uart.Write(writeBuffer);

            // Wait a bit for the data to loop back
            Thread.Sleep(100);

            // Read back
            byte[] readBuffer = new byte[writeBuffer.Length];
            int bytesRead = uart.Read(readBuffer);

            if (bytesRead > 0)
            {
                string received = Encoding.ASCII.GetString(readBuffer, 0, bytesRead);
                Console.WriteLine($"Received: \"{received}\"");

                if (testMessage == received)
                {
                    Console.WriteLine("✓ Loopback test PASSED!");
                }
                else
                {
                    Console.WriteLine("✗ Loopback test FAILED - data mismatch");
                }
            }
            else
            {
                Console.WriteLine("✗ No data received - check TX/RX connection");
            }
        }
        Console.ReadKey();
    }

    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey();
}

void TestCombined(Ft232RDevice ft232r)
{
    Console.WriteLine("\n====== Combined UART + GPIO Test ======");
    Console.WriteLine("This demonstrates using UART and 4 GPIO pins simultaneously");
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey();

    // Create UART
    var settings = new UartConnectionSettings("FT232R")
    {
        BaudRate = 9600,
        DataBits = 8,
        Parity = UartParity.None,
        StopBits = UartStopBits.Two
    };

    using (var uart = ft232r.CreateUartDevice(settings))
    {
        var gpioController = ft232r.CreateGpioController();

        // Setup CBUS0 as output (LED or indicator)
        int ledPin = 0;
        gpioController.OpenPin(ledPin);
        gpioController.SetPinMode(ledPin, PinMode.Output);

        Console.WriteLine("UART + GPIO active");
        Console.WriteLine("CBUS0 will blink while echoing UART data");
        Console.WriteLine("Send data via UART to see it echoed back");
        Console.WriteLine("Press Ctrl+C to exit");

        int counter = 0;
        while (true)
        {
            // Blink LED
            gpioController.Write(ledPin, counter++ % 2 == 0 ? PinValue.High : PinValue.Low);

            // Check for UART data
            if (uart.BytesToRead > 0)
            {
                byte[] buffer = new byte[uart.BytesToRead];
                int bytesRead = uart.Read(buffer);

                if (bytesRead > 0)
                {
                    // Echo back
                    uart.Write(buffer.AsSpan(0, bytesRead));
                    Console.WriteLine($"Echoed {bytesRead} bytes");
                }
            }

            Thread.Sleep(200);
        }
    }
}
