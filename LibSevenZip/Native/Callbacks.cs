using System.Runtime.InteropServices;

namespace LibSevenZip.Native;

/// <summary>
/// IArchiveOpenCallback. Only used for cancellation; 7-Zip asks it for
/// IArchiveOpenVolumeCallback / ICryptoGetTextPassword, which aren't supported, so
/// multi-volume archives and archives with encrypted headers won't open.
/// </summary>
internal sealed unsafe class OpenCallback : ComCallable
{
    private static readonly void** Vtbl = CreateOpenCallbackVtbl();

    private readonly CallbackErrors _errors;
    private readonly CancellationToken _ct;

    public OpenCallback(CallbackErrors errors, CancellationToken ct)
        : base(Vtbl)
    {
        _errors = errors;
        _ct = ct;
    }

    protected override bool Supports(in Guid iid) => iid == Iid.IArchiveOpenCallback;

    private static void** CreateOpenCallbackVtbl()
    {
        var vtbl = CreateVtbl(5);
        vtbl[3] = (delegate* unmanaged<void*, ulong*, ulong*, int>)&Progress;
        vtbl[4] = (delegate* unmanaged<void*, ulong*, ulong*, int>)&Progress;
        return vtbl;
    }

    [UnmanagedCallersOnly]
    private static int Progress(void* self, ulong* files, ulong* bytes)
    {
        var callback = Get<OpenCallback>(self);
        return callback._ct.IsCancellationRequested
            ? callback._errors.Record(new OperationCanceledException(callback._ct))
            : HResult.S_OK;
    }
}

/// <summary>Managed side of an extraction, driven by <see cref="ExtractCallback"/>.</summary>
internal interface IExtractHandler
{
    void SetTotal(ulong total);

    void SetCompleted(ulong completed);

    /// <summary>Returns the stream to write the item to, or null to skip its data.</summary>
    Stream? GetStream(uint index, AskMode askMode);

    void PrepareOperation(AskMode askMode);

    void SetOperationResult(OperationResult result);
}

/// <summary>IArchiveExtractCallback (IProgress + GetStream/PrepareOperation/SetOperationResult).</summary>
internal sealed unsafe class ExtractCallback : ComCallable
{
    private static readonly void** Vtbl = CreateExtractCallbackVtbl();

    private readonly IExtractHandler _handler;
    private readonly CallbackErrors _errors;
    private readonly CancellationToken _ct;

    public ExtractCallback(IExtractHandler handler, CallbackErrors errors, CancellationToken ct)
        : base(Vtbl)
    {
        _handler = handler;
        _errors = errors;
        _ct = ct;
    }

    protected override bool Supports(in Guid iid) =>
        iid == Iid.IProgress || iid == Iid.IArchiveExtractCallback;

    private static void** CreateExtractCallbackVtbl()
    {
        var vtbl = CreateVtbl(8);
        // IProgress
        vtbl[3] = (delegate* unmanaged<void*, ulong, int>)&SetTotal;
        vtbl[4] = (delegate* unmanaged<void*, ulong*, int>)&SetCompleted;
        // IArchiveExtractCallback
        vtbl[5] = (delegate* unmanaged<void*, uint, void**, int, int>)&GetStream;
        vtbl[6] = (delegate* unmanaged<void*, int, int>)&PrepareOperation;
        vtbl[7] = (delegate* unmanaged<void*, int, int>)&SetOperationResult;
        return vtbl;
    }

    [UnmanagedCallersOnly]
    private static int SetTotal(void* self, ulong total)
    {
        var callback = Get<ExtractCallback>(self);
        try
        {
            callback._handler.SetTotal(total);
            return HResult.S_OK;
        }
        catch (Exception e)
        {
            return callback._errors.Record(e);
        }
    }

    [UnmanagedCallersOnly]
    private static int SetCompleted(void* self, ulong* completeValue)
    {
        var callback = Get<ExtractCallback>(self);
        try
        {
            callback._ct.ThrowIfCancellationRequested();
            if (completeValue is not null)
            {
                callback._handler.SetCompleted(*completeValue);
            }

            return HResult.S_OK;
        }
        catch (Exception e)
        {
            return callback._errors.Record(e);
        }
    }

    [UnmanagedCallersOnly]
    private static int GetStream(void* self, uint index, void** outStream, int askExtractMode)
    {
        *outStream = null;
        var callback = Get<ExtractCallback>(self);
        try
        {
            callback._ct.ThrowIfCancellationRequested();
            var stream = callback._handler.GetStream(index, (AskMode)askExtractMode);
            if (stream is not null)
            {
                // the reference we hold at creation is handed to 7-Zip, which releases it
                *outStream = new OutStream(stream, callback._errors, callback._ct).Detach();
            }

            return HResult.S_OK;
        }
        catch (Exception e)
        {
            return callback._errors.Record(e);
        }
    }

    [UnmanagedCallersOnly]
    private static int PrepareOperation(void* self, int askExtractMode)
    {
        var callback = Get<ExtractCallback>(self);
        try
        {
            callback._handler.PrepareOperation((AskMode)askExtractMode);
            return HResult.S_OK;
        }
        catch (Exception e)
        {
            return callback._errors.Record(e);
        }
    }

    [UnmanagedCallersOnly]
    private static int SetOperationResult(void* self, int opRes)
    {
        var callback = Get<ExtractCallback>(self);
        try
        {
            callback._handler.SetOperationResult((OperationResult)opRes);
            return HResult.S_OK;
        }
        catch (Exception e)
        {
            return callback._errors.Record(e);
        }
    }
}
