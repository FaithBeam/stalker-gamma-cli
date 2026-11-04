namespace LibSevenZip.Native;

/// <summary>
/// Calls into an IInArchive implemented by 7-Zip. Slots after IUnknown (0-2):
/// Open, Close, GetNumberOfItems, GetProperty, Extract, GetArchiveProperty,
/// GetNumberOfProperties, GetPropertyInfo, GetNumberOfArchiveProperties,
/// GetArchivePropertyInfo.
/// </summary>
internal readonly unsafe struct NativeInArchive
{
    private readonly void* _ptr;

    public NativeInArchive(void* ptr) => _ptr = ptr;

    private void** Vtbl => *(void***)_ptr;

    public uint Release() => ((delegate* unmanaged<void*, uint>)Vtbl[2])(_ptr);

    public int Open(void* stream, ulong* maxCheckStartPosition, void* openCallback) =>
        ((delegate* unmanaged<void*, void*, ulong*, void*, int>)Vtbl[3])(
            _ptr,
            stream,
            maxCheckStartPosition,
            openCallback
        );

    public int Close() => ((delegate* unmanaged<void*, int>)Vtbl[4])(_ptr);

    public int GetNumberOfItems(uint* numItems) =>
        ((delegate* unmanaged<void*, uint*, int>)Vtbl[5])(_ptr, numItems);

    public int GetProperty(uint index, ItemPropId propId, PropVariant* value) =>
        ((delegate* unmanaged<void*, uint, uint, PropVariant*, int>)Vtbl[6])(
            _ptr,
            index,
            (uint)propId,
            value
        );

    /// <param name="indices">Sorted item indices, or null with numItems = uint.MaxValue for all.</param>
    public int Extract(uint* indices, uint numItems, int testMode, void* extractCallback) =>
        ((delegate* unmanaged<void*, uint*, uint, int, void*, int>)Vtbl[7])(
            _ptr,
            indices,
            numItems,
            testMode,
            extractCallback
        );
}
