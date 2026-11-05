using System.Runtime.InteropServices;

namespace LibSevenZip.Native;

/// <summary>
/// First exception thrown by managed code called from 7-Zip. Exceptions must not cross
/// back into native code, so callbacks record them here, return a failure HRESULT, and
/// the caller rethrows once control is back in managed code.
/// </summary>
internal sealed class CallbackErrors
{
    private Exception? _exception;

    public Exception? Exception => Volatile.Read(ref _exception);

    public int Record(Exception e)
    {
        Interlocked.CompareExchange(ref _exception, e, null);
        return e is OperationCanceledException ? HResult.E_ABORT : HResult.E_FAIL;
    }
}

/// <summary>
/// A managed object exposed to 7-Zip as a COM interface pointer.
///
/// Layout handed to native code: { vtable*, GCHandle, refCount }. The GCHandle keeps the
/// managed object alive for as long as 7-Zip holds a reference. The creator owns the
/// initial reference and gives it up with <see cref="Release"/> (or by handing the
/// pointer to 7-Zip as an out parameter, which transfers it).
///
/// Every object implements a single interface chain (IUnknown → ... → leaf), so one
/// vtable and one pointer serve all of its supported IIDs.
/// </summary>
internal abstract unsafe class ComCallable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Block
    {
        public void** Vtbl;
        public nint Handle;
        public int RefCount;
    }

    private readonly Block* _block;
    private int _creatorReleased;

    protected ComCallable(void** vtbl)
    {
        _block = (Block*)NativeMemory.AllocZeroed((nuint)sizeof(Block));
        _block->Vtbl = vtbl;
        _block->RefCount = 1;
        _block->Handle = GCHandle.ToIntPtr(GCHandle.Alloc(this));
    }

    /// <summary>The interface pointer. Valid while anyone holds a reference.</summary>
    public void* Pointer => _block;

    /// <summary>Gives up the creator's reference.</summary>
    public void Release()
    {
        if (Interlocked.Exchange(ref _creatorReleased, 1) == 0)
        {
            ReleaseCore(_block);
        }
    }

    /// <summary>Marks the creator's reference as handed over to native code.</summary>
    public void* Detach()
    {
        Interlocked.Exchange(ref _creatorReleased, 1);
        return _block;
    }

    protected abstract bool Supports(in Guid iid);

    protected static T Get<T>(void* self)
        where T : ComCallable => (T)GCHandle.FromIntPtr(((Block*)self)->Handle).Target!;

    /// <summary>Allocates a vtable with IUnknown filled in. Lives for the process lifetime.</summary>
    protected static void** CreateVtbl(int slots)
    {
        var vtbl = (void**)NativeMemory.AllocZeroed((nuint)(slots * sizeof(void*)));
        vtbl[0] = (delegate* unmanaged<void*, Guid*, void**, int>)&QueryInterface;
        vtbl[1] = (delegate* unmanaged<void*, uint>)&AddRef;
        vtbl[2] = (delegate* unmanaged<void*, uint>)&ReleaseNative;
        return vtbl;
    }

    [UnmanagedCallersOnly]
    private static int QueryInterface(void* self, Guid* iid, void** ppv)
    {
        if (ppv is null)
        {
            return HResult.E_INVALIDARG;
        }

        try
        {
            if (*iid == Iid.IUnknown || Get<ComCallable>(self).Supports(*iid))
            {
                Interlocked.Increment(ref ((Block*)self)->RefCount);
                *ppv = self;
                return HResult.S_OK;
            }
        }
        catch
        {
            // fall through
        }

        *ppv = null;
        return HResult.E_NOINTERFACE;
    }

    [UnmanagedCallersOnly]
    private static uint AddRef(void* self) =>
        (uint)Interlocked.Increment(ref ((Block*)self)->RefCount);

    [UnmanagedCallersOnly]
    private static uint ReleaseNative(void* self) => ReleaseCore((Block*)self);

    private static uint ReleaseCore(Block* block)
    {
        var count = Interlocked.Decrement(ref block->RefCount);
        if (count == 0)
        {
            GCHandle.FromIntPtr(block->Handle).Free();
            NativeMemory.Free(block);
        }

        return (uint)count;
    }
}
