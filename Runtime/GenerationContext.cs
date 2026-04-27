using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AOT;
using Cysharp.Threading.Tasks;

public abstract class GenerationContext : IDisposable
{
    private static readonly ConcurrentDictionary<IntPtr, OperationState> _states = new();

    private sealed class OperationState
    {
        public Action<string> OnToken;
        public UniTaskCompletionSource<ResponseCode> Completion;
    }

    private readonly Engine _parentEngine;
    private readonly string _objectName;
    private IntPtr _handle;
    private int _disposed;

    protected GenerationContext(Engine parentEngine, IntPtr handle, string objectName)
    {
        _parentEngine = parentEngine;
        _handle = handle;
        _objectName = objectName;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TokenCallback(IntPtr session, byte* data, int length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void FinalCallback(IntPtr session, int result);

    internal static readonly unsafe TokenCallback tokenCallback = OnTokenCallback;
    internal static readonly FinalCallback finalCallback = OnFinalCallback;

    protected IntPtr Handle
    {
        get
        {
            if (_handle == IntPtr.Zero || Volatile.Read(ref _disposed) == 1)
            {
                throw new ObjectDisposedException(_objectName);
            }

            return _handle;
        }
    }

    internal async UniTask<ResponseCode> RunStreamingOperationAsync(
        Action<IntPtr, TokenCallback, FinalCallback> startOperation,
        Action<string> onToken = null,
        CancellationToken cancellationToken = default)
    {
        IntPtr handle = Handle;
        cancellationToken.ThrowIfCancellationRequested();

        var state = new OperationState
        {
            OnToken = onToken,
            Completion = new UniTaskCompletionSource<ResponseCode>()
        };

        if (!_states.TryAdd(handle, state))
        {
            throw new InvalidOperationException("Only one operation can be active on a context at a time.");
        }

        CancellationTokenRegistration registration = default;

        try
        {
            if (cancellationToken.CanBeCanceled && SupportsCancellation)
            {
                registration = cancellationToken.Register(() =>
                {
                    if (!_states.ContainsKey(handle))
                    {
                        return;
                    }

                    CancelOperation(handle);
                });
            }

            startOperation(handle, tokenCallback, finalCallback);

            ResponseCode code = await state.Completion.Task;

            await UniTask.SwitchToMainThread();

            if (code == ResponseCode.kCancelled)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (code != ResponseCode.kOk)
            {
                throw new Exception($"Operation failed: {code}");
            }

            return code;
        }
        catch
        {
            _states.TryRemove(handle, out _);
            throw;
        }
        finally
        {
            registration.Dispose();
        }
    }

    internal async UniTask<ResponseCode> RunCompletionOperationAsync(Func<IntPtr, FinalCallback, int> startOperation)
    {
        IntPtr handle = Handle;

        var state = new OperationState
        {
            Completion = new UniTaskCompletionSource<ResponseCode>()
        };

        if (!_states.TryAdd(handle, state))
        {
            throw new InvalidOperationException("Only one operation can be active on a context at a time.");
        }

        try
        {
            int rc = startOperation(handle, finalCallback);
            if (rc != 0)
            {
                _states.TryRemove(handle, out _);
                throw new Exception($"Operation failed to start (rc={rc})");
            }

            ResponseCode code = await state.Completion.Task;

            await UniTask.SwitchToMainThread();

            if (code == ResponseCode.kCancelled)
            {
                throw new OperationCanceledException("Operation cancelled.");
            }

            if (code != ResponseCode.kOk)
            {
                throw new Exception($"Operation failed: {code}");
            }

            return code;
        }
        catch
        {
            _states.TryRemove(handle, out _);
            throw;
        }
    }

    [MonoPInvokeCallback(typeof(TokenCallback))]
    private static unsafe void OnTokenCallback(IntPtr handle, byte* data, int length)
    {
        if (handle == IntPtr.Zero) return;
        if (data == null || length <= 0) return;
        if (!_states.TryGetValue(handle, out _)) return;

        string token = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data, length));
        ProcessTokenAsync(handle, token).Forget();
    }

    private static async UniTask ProcessTokenAsync(IntPtr handle, string token)
    {
        await UniTask.SwitchToMainThread();

        Action<string> cb = null;
        if (_states.TryGetValue(handle, out var state))
        {
            cb = state.OnToken;
        }

        try
        {
            cb?.Invoke(token);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
        }
    }

    [MonoPInvokeCallback(typeof(FinalCallback))]
    private static void OnFinalCallback(IntPtr handle, int result)
    {
        if (handle == IntPtr.Zero) return;
        if (!_states.TryRemove(handle, out var state)) return;

        state.Completion.TrySetResult((ResponseCode)result);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        IntPtr handle = _handle;
        if (handle == IntPtr.Zero) return;

        OperationState pendingState = null;

        if (SupportsCancellation)
        {
            try
            {
                if (_states.ContainsKey(handle))
                {
                    CancelOperation(handle);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Exception while cancelling operation: {ex}");
            }
        }

        try
        {
            _parentEngine.WaitUntilDone(10000);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"WaitUntilDone threw: {ex}");
        }

        try
        {
            _states.TryRemove(handle, out pendingState);
            DestroyHandle(handle);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"DestroyHandle threw: {ex}");
        }
        finally
        {
            _handle = IntPtr.Zero;
            pendingState?.Completion.TrySetResult(ResponseCode.kCancelled);
        }
    }

    protected virtual bool SupportsCancellation => false;

    protected abstract void DestroyHandle(IntPtr handle);

    protected abstract void CancelOperation(IntPtr handle);
}
