// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Device;
using System.Device.Gpio;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Iot.Device.FtCommon;

namespace Iot.Device.Ft232R
{
    /// <summary>
    /// GPIO driver for the Ft232R
    /// </summary>
    public class Ft232RGpio : GpioDriver
    {
        private readonly Dictionary<int, PinValue> _pinValues = new Dictionary<int, PinValue>();

        /// <summary>
        /// Store the FTDI Device Information
        /// </summary>
        public Ft232RDevice DeviceInformation { get; private set; }

        /// <inheritdoc/>
        protected override int PinCount => DeviceInformation.PinCount;

        /// <summary>
        /// Creates a GPIO Driver
        /// </summary>
        /// <param name="deviceInformation">The Ft232R device</param>
        internal Ft232RGpio(Ft232RDevice deviceInformation)
        {
            DeviceInformation = deviceInformation;
            // Open device
            DeviceInformation.GetHandle();
            DeviceInformation.InitializeGpio();
        }

        /// <inheritdoc/>
        protected override int ConvertPinNumberToLogicalNumberingScheme(int pinNumber) => pinNumber;

        /// <inheritdoc/>
        protected override void OpenPin(int pinNumber)
        {
            if ((pinNumber < 0) || (pinNumber >= PinCount))
            {
                throw new ArgumentException($"Pin number can only be between 0 and {PinCount - 1}");
            }

            if (DeviceInformation.PinOpen[pinNumber])
            {
                throw new ArgumentException($"Pin {pinNumber} is already open");
            }

            DeviceInformation.PinOpen[pinNumber] = true;
            _pinValues.TryAdd(pinNumber, PinValue.Low);
        }

        /// <inheritdoc/>
        protected override void ClosePin(int pinNumber)
        {
            DeviceInformation.PinOpen[pinNumber] = false;
            _pinValues.Remove(pinNumber);
        }

        /// <inheritdoc/>
        protected override void SetPinMode(int pinNumber, PinMode mode)
        {
            if ((pinNumber < 0) || (pinNumber >= PinCount))
            {
                throw new ArgumentException($"Pin number can only be between 0 and {PinCount - 1}");
            }

            // CbusMask format: MMMMVVVV
            // Upper nibble (bits 4-7) = direction (0=input, 1=output)
            // Lower nibble (bits 0-3) = value (0=low, 1=high)

            if (mode == PinMode.Output)
            {
                // Set direction bit in upper nibble
                DeviceInformation.CbusMask |= (byte)(1 << (pinNumber + 4));
            }
            else
            {
                // Clear direction bit in upper nibble
                DeviceInformation.CbusMask &= (byte)(~(1 << (pinNumber + 4)));
            }

            DeviceInformation.SetGpioValues();
        }

        /// <inheritdoc/>
        protected override PinMode GetPinMode(int pinNumber)
        {
            if ((pinNumber < 0) || (pinNumber >= PinCount))
            {
                throw new ArgumentException($"Pin number can only be between 0 and {PinCount - 1}");
            }

            // Check upper nibble for direction
            return ((DeviceInformation.CbusMask >> (pinNumber + 4)) & 0x01) == 0x01 ? PinMode.Output : PinMode.Input;
        }

        /// <inheritdoc/>
        protected override bool IsPinModeSupported(int pinNumber, PinMode mode)
        {
            return mode == PinMode.Input || mode == PinMode.Output;
        }

        /// <inheritdoc/>
        protected override PinValue Read(int pinNumber)
        {
            if ((pinNumber < 0) || (pinNumber >= PinCount))
            {
                throw new ArgumentException($"Pin number can only be between 0 and {PinCount - 1}");
            }

            var val = DeviceInformation.GetGpioValues();
            _pinValues[pinNumber] = (((val >> pinNumber) & 0x01) == 0x01) ? PinValue.High : PinValue.Low;

            return _pinValues[pinNumber];
        }

        /// <inheritdoc/>
        protected override void Toggle(int pinNumber) => Write(pinNumber, !_pinValues[pinNumber]);

        /// <inheritdoc/>
        protected override void Write(int pinNumber, PinValue value)
        {
            if ((pinNumber < 0) || (pinNumber >= PinCount))
            {
                throw new ArgumentException($"Pin number can only be between 0 and {PinCount - 1}");
            }

            // CbusMask format: MMMMVVVV
            // Lower nibble (bits 0-3) = value (0=low, 1=high)

            if (value == PinValue.High)
            {
                // Set value bit in lower nibble
                DeviceInformation.CbusMask |= (byte)(1 << pinNumber);
            }
            else
            {
                // Clear value bit in lower nibble
                DeviceInformation.CbusMask &= (byte)(~(1 << pinNumber));
            }

            DeviceInformation.SetGpioValues();
            _pinValues[pinNumber] = value;
        }

        /// <inheritdoc/>
        protected override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        protected override void AddCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        protected override void RemoveCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override ComponentInformation QueryComponentInformation()
        {
            var ret = base.QueryComponentInformation();
            ret.Properties["Description"] = DeviceInformation.Description;
            ret.Properties["SerialNumber"] = DeviceInformation.SerialNumber;
            return ret;
        }
    }
}
