// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Device.Uart;
using System.IO;
using Iot.Device.FtCommon;

namespace Iot.Device.Ft232R
{
    /// <summary>
    /// UART implementation for FT232R
    /// </summary>
    public class Ft232RUart : UartDevice
    {
        private readonly Ft232RDevice _device;
        private readonly UartConnectionSettings _settings;

        /// <summary>
        /// Creates a new instance of <see cref="Ft232RUart"/>
        /// </summary>
        /// <param name="device">The FT232R device</param>
        /// <param name="settings">The UART connection settings</param>
        internal Ft232RUart(Ft232RDevice device, UartConnectionSettings settings)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _settings = new UartConnectionSettings(settings);

            // Initialize UART with the specified settings
            InitializeUart();
        }

        /// <inheritdoc/>
        public override UartConnectionSettings ConnectionSettings => new UartConnectionSettings(_settings);

        /// <inheritdoc/>
        public override int BytesToRead => (int)_device.GetAvailableBytes();

        /// <inheritdoc/>
        public override int BytesToWrite => 0; // FT232R doesn't provide this information easily

        private void InitializeUart()
        {
            _device.GetHandle();

            // Set baud rate
            var ftStatus = _device.SetBaudRate((uint)_settings.BaudRate);
            if (ftStatus != FtStatus.Ok)
            {
                throw new IOException($"Failed to set baud rate to {_settings.BaudRate}, status: {ftStatus}");
            }

            // Set data characteristics (data bits, stop bits, parity)
            ftStatus = _device.SetDataCharacteristics(_settings.DataBits, _settings.StopBits, _settings.Parity);
            if (ftStatus != FtStatus.Ok)
            {
                throw new IOException($"Failed to set data characteristics, status: {ftStatus}");
            }

            // Set flow control
            ftStatus = _device.SetFlowControl(_settings.FlowControl);
            if (ftStatus != FtStatus.Ok)
            {
                throw new IOException($"Failed to set flow control, status: {ftStatus}");
            }

            // Set timeouts
            ftStatus = _device.SetTimeouts(_settings.ReadTimeout, _settings.WriteTimeout);
            if (ftStatus != FtStatus.Ok)
            {
                throw new IOException($"Failed to set timeouts, status: {ftStatus}");
            }
        }

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return 0;
            }

            return _device.ReadUart(buffer);
        }

        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            _device.WriteUart(buffer);
        }

        /// <inheritdoc/>
        public override void DiscardInBuffer()
        {
            _device.PurgeRx();
        }

        /// <inheritdoc/>
        public override void DiscardOutBuffer()
        {
            _device.PurgeTx();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Nothing specific to dispose for UART
                // The device handle is managed by Ft232RDevice
            }

            base.Dispose(disposing);
        }
    }
}
