// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace System.Device.Uart;

/// <summary>
/// The communications channel to a UART device.
/// </summary>
public abstract class UartDevice : IDisposable
{
    /// <summary>
    /// The connection settings of the UART device. The connection settings are immutable after the device is created
    /// so the object returned will be a clone of the settings object.
    /// </summary>
    public abstract UartConnectionSettings ConnectionSettings { get; }

    /// <summary>
    /// Gets the number of bytes available to read from the input buffer.
    /// </summary>
    public abstract int BytesToRead { get; }

    /// <summary>
    /// Gets the number of bytes in the output buffer waiting to be sent.
    /// </summary>
    public abstract int BytesToWrite { get; }

    /// <summary>
    /// Reads a byte from the UART device.
    /// </summary>
    /// <returns>A byte read from the UART device, or -1 if no data is available.</returns>
    public virtual int ReadByte()
    {
        Span<byte> buffer = stackalloc byte[1];
        int bytesRead = Read(buffer);
        return bytesRead > 0 ? buffer[0] : -1;
    }

    /// <summary>
    /// Reads data from the UART device.
    /// </summary>
    /// <param name="buffer">
    /// The buffer to read the data from the UART device.
    /// The length of the buffer determines how much data to read from the UART device.
    /// </param>
    /// <returns>The number of bytes actually read.</returns>
    public abstract int Read(Span<byte> buffer);

    /// <summary>
    /// Reads data from the UART device asynchronously.
    /// </summary>
    /// <param name="buffer">The buffer to read the data into.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of bytes actually read.</returns>
    public virtual Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Read(buffer.Span), cancellationToken);
    }

    /// <summary>
    /// Writes a byte to the UART device.
    /// </summary>
    /// <param name="value">The byte to be written to the UART device.</param>
    public virtual void WriteByte(byte value)
    {
        ReadOnlySpan<byte> buffer = stackalloc byte[1] { value };
        Write(buffer);
    }

    /// <summary>
    /// Writes data to the UART device.
    /// </summary>
    /// <param name="buffer">
    /// The buffer that contains the data to be written to the UART device.
    /// </param>
    public abstract void Write(ReadOnlySpan<byte> buffer);

    /// <summary>
    /// Writes data to the UART device asynchronously.
    /// </summary>
    /// <param name="buffer">The buffer that contains the data to be written.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public virtual Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Write(buffer.Span), cancellationToken);
    }

    /// <summary>
    /// Discards data from the input buffer.
    /// </summary>
    public abstract void DiscardInBuffer();

    /// <summary>
    /// Discards data from the output buffer.
    /// </summary>
    public abstract void DiscardOutBuffer();

    /// <summary>
    /// Waits for all data in the output buffer to be transmitted.
    /// </summary>
    public virtual void Flush()
    {
        // Default implementation: wait until BytesToWrite is 0
        while (BytesToWrite > 0)
        {
            Thread.Sleep(1);
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes this instance
    /// </summary>
    /// <param name="disposing"><see langword="true"/> if explicitly disposing, <see langword="false"/> if in finalizer</param>
    protected virtual void Dispose(bool disposing)
    {
        // Nothing to do in base class.
    }
}
