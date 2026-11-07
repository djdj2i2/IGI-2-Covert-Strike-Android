namespace ProjectIGI.Rendering.Pipeline;

/// <summary>Sealing what a frame has recorded so far, so its finish overlaps what follows.</summary>
public sealed partial class GraphicsDevice
{
    /// <inheritdoc/>
    public void SealRecordedFrame(string label)
    {
        ThrowIfDisposed();

        // Only the direct present pass closes here: a scene pass or a shadow pass is still being
        // recorded, and SealEncoder refuses to seal while one is open, so on the deferred path this
        // is a no-op. The next draw reopens the present pass over what it had drawn.
        ClosePass();
        SealEncoder(label);
    }
}
