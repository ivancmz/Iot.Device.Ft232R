// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace Iot.Device.FtCommon
{
    /// <summary>
    /// Bit Mode used for pins.
    /// </summary>
    internal enum FtBitMode : byte
    {
        /// <summary>Reset the IO Bit Mode.</summary>
        ResetIoBitMode = 0x00,

        /// <summary>Asynchronous Bit Bang Mode.</summary>
        AsynchronousBitBang = 0x01,

        /// <summary>MPSSE.</summary>
        Mpsee = 0x02,

        /// <summary>Synchronous Bit Bang Mode.</summary>
        SynchronousBitBang = 0x04,

        /// <summary>MCU Host Bus Emulation.</summary>
        McuHostBusEmulation = 0x08,

        /// <summary>Fast Serial For Opto-Isolation.</summary>
        FastSerialForOptoIsolation = 0x10,

        /// <summary>CBUS Bit Bang Mode (FT232R only).</summary>
        CbusBitBang = 0x20,
    }
}
