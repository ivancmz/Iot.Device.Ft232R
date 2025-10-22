// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Device.Uart;

/// <summary>
/// Parity type for UART communication
/// </summary>
public enum UartParity
{
    /// <summary>No parity bit</summary>
    None,
    /// <summary>Odd parity</summary>
    Odd,
    /// <summary>Even parity</summary>
    Even,
    /// <summary>Mark parity (always 1)</summary>
    Mark,
    /// <summary>Space parity (always 0)</summary>
    Space
}

/// <summary>
/// Number of stop bits for UART communication
/// </summary>
public enum UartStopBits
{
    /// <summary>1 stop bit</summary>
    One,
    /// <summary>1.5 stop bits</summary>
    OnePointFive,
    /// <summary>2 stop bits</summary>
    Two
}

/// <summary>
/// Flow control type for UART communication
/// </summary>
public enum UartFlowControl
{
    /// <summary>No flow control</summary>
    None,
    /// <summary>Hardware flow control (RTS/CTS)</summary>
    RequestToSend,
    /// <summary>Software flow control (XON/XOFF)</summary>
    XOnXOff
}

/// <summary>
/// The connection settings for a UART device.
/// </summary>
public sealed class UartConnectionSettings
{
    private UartConnectionSettings()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UartConnectionSettings"/> class.
    /// </summary>
    /// <param name="portName">The port name or device path (e.g., "COM1" on Windows, "/dev/ttyUSB0" on Linux).</param>
    public UartConnectionSettings(string portName)
    {
        PortName = portName ?? throw new ArgumentNullException(nameof(portName));
    }

    /// <summary>
    /// Copy constructor
    /// </summary>
    internal UartConnectionSettings(UartConnectionSettings other)
    {
        PortName = other.PortName;
        BaudRate = other.BaudRate;
        DataBits = other.DataBits;
        Parity = other.Parity;
        StopBits = other.StopBits;
        FlowControl = other.FlowControl;
        ReadTimeout = other.ReadTimeout;
        WriteTimeout = other.WriteTimeout;
    }

    /// <summary>
    /// The port name or device path.
    /// </summary>
    public string PortName { get; set; }

    /// <summary>
    /// The baud rate (bits per second). Default is 9600.
    /// </summary>
    /// <remarks>Common values: 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600</remarks>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// The number of data bits. Default is 8.
    /// </summary>
    /// <remarks>Valid values are typically 5, 6, 7, or 8.</remarks>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// The parity type. Default is None.
    /// </summary>
    public UartParity Parity { get; set; } = UartParity.None;

    /// <summary>
    /// The number of stop bits. Default is One.
    /// </summary>
    public UartStopBits StopBits { get; set; } = UartStopBits.One;

    /// <summary>
    /// The flow control type. Default is None.
    /// </summary>
    public UartFlowControl FlowControl { get; set; } = UartFlowControl.None;

    /// <summary>
    /// The read timeout in milliseconds. Default is -1 (infinite).
    /// </summary>
    public int ReadTimeout { get; set; } = -1;

    /// <summary>
    /// The write timeout in milliseconds. Default is -1 (infinite).
    /// </summary>
    public int WriteTimeout { get; set; } = -1;
}
