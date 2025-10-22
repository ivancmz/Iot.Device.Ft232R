// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Iot.Device.Ft232R;
using Iot.Device.Board;
using Iot.Device.FtCommon;

namespace Iot.Device.Ft232R
{
    /// <summary>
    /// FT232R base Device
    /// </summary>
    public class Ft232RDevice : FtDevice, IDisposable
    {
        /// <summary>
        /// Number of pins for FT232R (4 CBUS pins: CBUS0-3)
        /// </summary>
        internal const int PinCountConst = 4;

        private SafeFtHandle _ftHandle = null!;

        // This is used by FT232R to track the GPIO states
        // Format: MMMMVVVV where M=direction (0=input, 1=output), V=value (0=low, 1=high)
        // Only lower 4 bits are used for CBUS0-3
        internal byte CbusMask = 0x00; // Combined direction and value in MMMMVVVV format

        internal bool[] PinOpen = new bool[PinCountConst];
        internal PinMode[] GpioDirections = new PinMode[PinCountConst];

        ///// <summary>
        ///// Gets all the Ft232R connected
        ///// </summary>
        ///// <returns>A list of Ft232R</returns>
        //public static List<Ft232RDevice> GetFt232R()
        //{
        //    List<Ft232RDevice> ft232s = new List<Ft232RDevice>();
        //    var devices = FtCommon.FtCommon.GetDevices().FindAll(d => d.Type == FtDeviceType.Ft232ROrFt245R);
        //    foreach (var device in devices)
        //    {
        //        ft232s.Add(new Ft232RDevice(device));
        //    }

        //    return ft232s;
        //}

        /// <summary>
        /// Gets all the FT232R connected
        /// </summary>
        /// <returns>A list of FT232R</returns>
        public static List<Ft232RDevice> GetFT232R()
        {
            List<Ft232RDevice> ft232s = new List<Ft232RDevice>();
            var devices = FtCommon.FtCommon.GetDevices();

            // Filter only FT232R devices
            foreach (var device in devices)
            {
                if (device.Type == FtDeviceType.Ft232ROrFt245R)
                {
                    ft232s.Add(new Ft232RDevice(device));
                }
            }

            return ft232s;
        }

        /// <summary>
        /// Gets the pin number from a string
        /// </summary>
        /// <param name="pin">A string</param>
        /// <returns>The pin number, -1 in case it's not found.</returns>
        /// <remarks>Valid pins are CBUS0 to 4, CB0 to 4 (FT232R has only 5 CBUS pins)</remarks>
        public static int GetPinNumberFromString(string pin)
        {
            pin = pin.ToUpper();
            switch (pin)
            {
                case "CBUS0":
                case "CB0":
                    return 0;
                case "CBUS1":
                case "CB1":
                    return 1;
                case "CBUS2":
                case "CB2":
                    return 2;
                case "CBUS3":
                case "CB3":
                    return 3;
                // Not available on FT232R with CBUS Bit Bang Mode
                //case "CBUS4":
                //case "CB4":
                //    return 4;
                default:
                    return -1;
            }

        }
        /// <summary>
        /// Instantiates a FT232R device object.
        /// </summary>
        /// <param name="flags">Indicates device state.</param>
        /// <param name="type">Indicates the device type.</param>
        /// <param name="id">The Vendor ID and Product ID of the device.</param>
        /// <param name="locId">The physical location identifier of the device.</param>
        /// <param name="serialNumber">The device serial number.</param>
        /// <param name="description">The device description.</param>
        public Ft232RDevice(FtFlag flags, FtDeviceType type, uint id, uint locId, string serialNumber, string description)
        : base(flags, type, id, locId, serialNumber, description)
        {
        }

        /// <summary>
        /// Instantiates a FT232R device object.
        /// </summary>
        /// <param name="ftDevice">a FT Device</param>
        public Ft232RDevice(FtDevice ftDevice)
            : this(ftDevice.Flags, ftDevice.Type, ftDevice.Id, ftDevice.LocId, ftDevice.SerialNumber, ftDevice.Description)
        {
        }

        /// <summary>
        /// Gets the number of pins for this specific FT device.
        /// </summary>
        public int PinCount => PinCountConst;

        /// <summary>
        /// Creates the <see cref="Ft232RGpio"/> controller
        /// </summary>
        /// <returns>A new GPIO driver</returns>
        protected override GpioDriver? TryCreateBestGpioDriver()
        {
            return new Ft232RGpio(this);
        }

        internal void GetHandle()
        {
            if ((_ftHandle == null) || _ftHandle.IsClosed)
            {
                // Open device if not opened
                var ftStatus = FtFunction.FT_OpenEx(LocId, FtOpenType.OpenByLocation, out _ftHandle);

                if (ftStatus != FtStatus.Ok)
                {
                    throw new IOException($"Failed to open device {Description}, status: {ftStatus}");
                }
            }
        }

        /// <summary>
        /// Resets the device.
        /// </summary>
        public void Reset()
        {
            GetHandle();
            FtFunction.FT_ResetDevice(_ftHandle);
        }


        #region gpio

        internal void InitializeGpio()
        {
            GetHandle();

            // Reset to default mode first
            var ftStatus = FtFunction.FT_SetBitMode(_ftHandle, 0x00, FtBitMode.ResetIoBitMode);

            if (ftStatus != FtStatus.Ok)
            {
                throw new IOException($"Failed to reset device {Description}, status: {ftStatus}");
            }

            // Initialize CBUS mask: all pins as inputs (direction=0), all values low (value=0)
            CbusMask = 0x00; // MMMMVVVV = 00000000

            // Enable CBUS Bit Bang mode for FT232R
            ftStatus = FtFunction.FT_SetBitMode(_ftHandle, CbusMask, FtBitMode.CbusBitBang);

            if (ftStatus != FtStatus.Ok)
            {
                throw new IOException($"Failed to setup device {Description}, status: {ftStatus} in CBUS bitbang mode");
            }

            // Discard any pending input
            DiscardInput();
        }

        internal byte GetGpioValues()
        {
            // Read the instantaneous value of the CBUS pins
            byte values = 0;
            var ftStatus = FtFunction.FT_GetBitMode(_ftHandle, ref values);

            if (ftStatus != FtStatus.Ok)
            {
                throw new IOException($"Failed to read GPIO values, status: {ftStatus}");
            }

            // Only lower 5 bits are valid for CBUS0-4
            return (byte)(values & 0x1F);
        }

        internal void SetGpioValues()
        {
            // Set both direction and values using CBUS Bit Bang mode
            // CbusMask format: MMMMVVVV
            // Upper nibble (MMMM) = direction mask (0=input, 1=output)
            // Lower nibble (VVVV) = value mask (0=low, 1=high)
            var ftStatus = FtFunction.FT_SetBitMode(_ftHandle, CbusMask, FtBitMode.CbusBitBang);

            if (ftStatus != FtStatus.Ok)
            {
                throw new IOException($"Failed to set GPIO values, status: {ftStatus}");
            }
        }

        #endregion

        #region uart

        /// <summary>
        /// Sets the baud rate for UART communication
        /// </summary>
        internal FtStatus SetBaudRate(uint baudRate)
        {
            return FtFunction.FT_SetBaudRate(_ftHandle, baudRate);
        }

        /// <summary>
        /// Sets data characteristics for UART communication
        /// </summary>
        internal FtStatus SetDataCharacteristics(int dataBits, System.Device.Uart.UartStopBits stopBits, System.Device.Uart.UartParity parity)
        {
            byte wordLength = (byte)dataBits;
            byte stopBitsValue = stopBits switch
            {
                System.Device.Uart.UartStopBits.One => 0,
                System.Device.Uart.UartStopBits.OnePointFive => 1,
                System.Device.Uart.UartStopBits.Two => 2,
                _ => throw new ArgumentException("Invalid stop bits value")
            };
            byte parityValue = parity switch
            {
                System.Device.Uart.UartParity.None => 0,
                System.Device.Uart.UartParity.Odd => 1,
                System.Device.Uart.UartParity.Even => 2,
                System.Device.Uart.UartParity.Mark => 3,
                System.Device.Uart.UartParity.Space => 4,
                _ => throw new ArgumentException("Invalid parity value")
            };

            return FtFunction.FT_SetDataCharacteristics(_ftHandle, wordLength, stopBitsValue, parityValue);
        }

        /// <summary>
        /// Sets flow control for UART communication
        /// </summary>
        internal FtStatus SetFlowControl(System.Device.Uart.UartFlowControl flowControl)
        {
            ushort flowControlValue = flowControl switch
            {
                System.Device.Uart.UartFlowControl.None => 0x0000,
                System.Device.Uart.UartFlowControl.RequestToSend => 0x0100, // RTS/CTS
                System.Device.Uart.UartFlowControl.XOnXOff => 0x0200, // XON/XOFF
                _ => throw new ArgumentException("Invalid flow control value")
            };

            // XON and XOFF characters (standard values)
            byte xon = 0x11;  // DC1
            byte xoff = 0x13; // DC3

            return FtFunction.FT_SetFlowControl(_ftHandle, flowControlValue, xon, xoff);
        }

        /// <summary>
        /// Sets read and write timeouts
        /// </summary>
        internal FtStatus SetTimeouts(int readTimeout, int writeTimeout)
        {
            uint readTimeoutMs = readTimeout < 0 ? 0 : (uint)readTimeout;
            uint writeTimeoutMs = writeTimeout < 0 ? 0 : (uint)writeTimeout;
            return FtFunction.FT_SetTimeouts(_ftHandle, readTimeoutMs, writeTimeoutMs);
        }

        /// <summary>
        /// Purges the RX buffer
        /// </summary>
        internal void PurgeRx()
        {
            const uint FT_PURGE_RX = 1;
            FtFunction.FT_Purge(_ftHandle, FT_PURGE_RX);
        }

        /// <summary>
        /// Purges the TX buffer
        /// </summary>
        internal void PurgeTx()
        {
            const uint FT_PURGE_TX = 2;
            FtFunction.FT_Purge(_ftHandle, FT_PURGE_TX);
        }

        /// <summary>
        /// Reads data from UART
        /// </summary>
        internal int ReadUart(Span<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return 0;
            }

            uint bytesRead = 0;
            var ftStatus = FtFunction.FT_Read(_ftHandle, in MemoryMarshal.GetReference(buffer), (uint)buffer.Length, ref bytesRead);

            if (ftStatus != FtStatus.Ok)
            {
                throw new IOException($"Failed to read from UART, status: {ftStatus}");
            }

            return (int)bytesRead;
        }

        /// <summary>
        /// Writes data to UART
        /// </summary>
        internal void WriteUart(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            uint bytesWritten = 0;
            var ftStatus = FtFunction.FT_Write(_ftHandle, in MemoryMarshal.GetReference(buffer), (uint)buffer.Length, ref bytesWritten);

            if (ftStatus != FtStatus.Ok || bytesWritten != buffer.Length)
            {
                throw new IOException($"Failed to write to UART, status: {ftStatus}");
            }
        }

        #endregion

        #region Read Write

        internal void Write(ReadOnlySpan<byte> buffer)
        {
            uint numBytesWritten = 0;
            var ftStatus = FtFunction.FT_Write(_ftHandle, in MemoryMarshal.GetReference(buffer), (ushort)buffer.Length, ref numBytesWritten);
            if ((ftStatus != FtStatus.Ok) || (buffer.Length != numBytesWritten))
            {
                throw new IOException($"Can't write to the device");
            }
        }

        internal int Read(Span<byte> buffer)
        {
            CancellationToken token = new CancellationTokenSource(1000).Token;
            int totalBytesRead = 0;
            uint bytesToRead = 0;
            uint numBytesRead = 0;
            FtStatus ftStatus;
            while ((totalBytesRead < buffer.Length) && (!token.IsCancellationRequested))
            {
                bytesToRead = GetAvailableBytes();
                if (bytesToRead > 0)
                {
                    ftStatus = FtFunction.FT_Read(_ftHandle, in buffer[totalBytesRead], bytesToRead, ref numBytesRead);
                    if ((ftStatus != FtStatus.Ok) && (bytesToRead != numBytesRead))
                    {
                        throw new IOException("Can't read device");
                    }

                    totalBytesRead += (int)numBytesRead;
                }
            }

            return totalBytesRead;
        }

        /// <summary>
        /// Clears all the input data to get an empty buffer.
        /// </summary>
        /// <returns>True for success</returns>
        private bool DiscardInput()
        {
            var availableBytes = GetAvailableBytes();

            if (availableBytes > 0)
            {
                byte[] toRead = new byte[availableBytes];
                uint bytesRead = 0;
                var ftStatus = FtFunction.FT_Read(_ftHandle, in toRead[0], availableBytes, ref bytesRead);
                return ftStatus == FtStatus.Ok;
            }

            return true;
        }

        internal uint GetAvailableBytes()
        {
            uint availableBytes = 0;
            var ftStatus = FtFunction.FT_GetQueueStatus(_ftHandle, ref availableBytes);
            if (ftStatus != FtStatus.Ok)
            {
                throw new IOException($"Can't get available bytes");
            }

            return availableBytes;
        }

        #endregion

        /// <summary>
        /// Creates a UART device for this FT232R
        /// </summary>
        /// <param name="settings">The UART connection settings</param>
        /// <returns>A UART device</returns>
        public Ft232RUart CreateUartDevice(System.Device.Uart.UartConnectionSettings settings)
        {
            return new Ft232RUart(this, settings);
        }

        /// <summary>
        /// Dispose FT232R device.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _ftHandle != null)
            {
                _ftHandle.Dispose();
                _ftHandle = null!;
            }

            base.Dispose(disposing);
        }
    }
}
