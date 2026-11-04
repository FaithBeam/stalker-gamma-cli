using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LibSevenZip.Native;

/// <summary>IInStream (ISequentialInStream + Seek) over a file handle.</summary>
internal sealed unsafe class InStream : ComCallable
{
    private static readonly void** Vtbl = CreateInStreamVtbl();

    private readonly SafeFileHandle _handle;
    private readonly CallbackErrors _errors;
    private readonly long _length;
    private long _position;

    public InStream(SafeFileHandle handle, CallbackErrors errors)
        : base(Vtbl)
    {
        _handle = handle;
        _errors = errors;
        _length = RandomAccess.GetLength(handle);
    }

    public void Rewind() => _position = 0;

    protected override bool Supports(in Guid iid) =>
        iid == Iid.ISequentialInStream || iid == Iid.IInStream;

    private static void** CreateInStreamVtbl()
    {
        var vtbl = CreateVtbl(5);
        vtbl[3] = (delegate* unmanaged<void*, void*, uint, uint*, int>)&Read;
        vtbl[4] = (delegate* unmanaged<void*, long, uint, ulong*, int>)&Seek;
        return vtbl;
    }

    [UnmanagedCallersOnly]
    private static int Read(void* self, void* data, uint size, uint* processedSize)
    {
        if (processedSize is not null)
        {
            *processedSize = 0;
        }

        var stream = Get<InStream>(self);
        try
        {
            if (size == 0)
            {
                return HResult.S_OK;
            }

            var read = RandomAccess.Read(
                stream._handle,
                new Span<byte>(data, (int)Math.Min(size, int.MaxValue)),
                stream._position
            );
            stream._position += read;
            if (processedSize is not null)
            {
                *processedSize = (uint)read;
            }

            return HResult.S_OK;
        }
        catch (Exception e)
        {
            return stream._errors.Record(e);
        }
    }

    [UnmanagedCallersOnly]
    private static int Seek(void* self, long offset, uint seekOrigin, ulong* newPosition)
    {
        var stream = Get<InStream>(self);
        var basePosition = seekOrigin switch
        {
            0 => 0,
            1 => stream._position,
            2 => stream._length,
            _ => -1,
        };
        if (basePosition < 0)
        {
            return HResult.STG_E_INVALIDFUNCTION;
        }

        var position = basePosition + offset;
        if (position < 0)
        {
            return HResult.STG_E_INVALIDFUNCTION;
        }

        stream._position = position;
        if (newPosition is not null)
        {
            *newPosition = (ulong)position;
        }

        return HResult.S_OK;
    }
}

/// <summary>ISequentialOutStream over a managed stream.</summary>
internal sealed unsafe class OutStream : ComCallable
{
    private static readonly void** Vtbl = CreateOutStreamVtbl();

    private readonly Stream _stream;
    private readonly CallbackErrors _errors;
    private readonly CancellationToken _ct;

    public OutStream(Stream stream, CallbackErrors errors, CancellationToken ct)
        : base(Vtbl)
    {
        _stream = stream;
        _errors = errors;
        _ct = ct;
    }

    protected override bool Supports(in Guid iid) => iid == Iid.ISequentialOutStream;

    private static void** CreateOutStreamVtbl()
    {
        var vtbl = CreateVtbl(4);
        vtbl[3] = (delegate* unmanaged<void*, void*, uint, uint*, int>)&Write;
        return vtbl;
    }

    [UnmanagedCallersOnly]
    private static int Write(void* self, void* data, uint size, uint* processedSize)
    {
        if (processedSize is not null)
        {
            *processedSize = 0;
        }

        var stream = Get<OutStream>(self);
        try
        {
            stream._ct.ThrowIfCancellationRequested();
            stream._stream.Write(new ReadOnlySpan<byte>(data, (int)Math.Min(size, int.MaxValue)));
            if (processedSize is not null)
            {
                *processedSize = size;
            }

            return HResult.S_OK;
        }
        catch (Exception e)
        {
            return stream._errors.Record(e);
        }
    }
}
