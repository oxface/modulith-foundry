namespace ModulithFoundry.ExecutionIdentity;

/// <summary>
/// A scope-owned context holder. Consumers establish it before starting business work
/// and dispose it after all operation branches finish. Reads are safe to perform concurrently.
/// </summary>
public sealed class OperationContextAccessor
    : IOperationContextAccessor,
        IOperationContextInitializer,
        IDisposable
{
    private readonly object _gate = new();
    private OperationContext? _context;
    private bool _disposed;

    public OperationContext Current
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _context
                    ?? throw new InvalidOperationException(
                        "Operation context has not been initialized."
                    );
            }
        }
    }

    public void Initialize(OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_context is not null)
            {
                throw new InvalidOperationException(
                    "Operation context has already been initialized."
                );
            }

            _context = context;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _context = null;
        }

        GC.SuppressFinalize(this);
    }
}
