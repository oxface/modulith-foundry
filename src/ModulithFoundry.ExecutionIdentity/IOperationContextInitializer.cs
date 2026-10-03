namespace ModulithFoundry.ExecutionIdentity;

/// <summary>Host-facing, single-assignment context establishment.</summary>
public interface IOperationContextInitializer
{
    /// <exception cref="ArgumentNullException">Context is null.</exception>
    /// <exception cref="InvalidOperationException">Context was already established.</exception>
    /// <exception cref="ObjectDisposedException">The owning accessor has been disposed.</exception>
    void Initialize(OperationContext context);
}
