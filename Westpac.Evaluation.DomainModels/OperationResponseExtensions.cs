namespace Westpac.Evaluation.DomainModels;

public static class OperationResponseExtensions
{
    
    
    public static async Task<TResult> Match<T, U, TResult>(
        this OperationResponse<T, U> responseTask,
        Func<T, Task<TResult>> onSuccess,
        Func<U, TResult> onError)
    {
        return responseTask switch
        {
            OperationResponse<T, U>.SuccessfulOperation success => await onSuccess(
                success.Data),
            OperationResponse<T, U>.FailedOperation failed => onError(
                failed.Data),
            _ => throw new InvalidOperationException("Unknown response type")
        };
    }
    
    
    public static async Task<TResult> Match<T, U, TResult>(
        this OperationResponse<T, U> responseTask,
        Func<T, TResult> onSuccess,
        Func<U, Task<TResult>> onError)
    {
        return responseTask switch
        {
            OperationResponse<T, U>.SuccessfulOperation success => onSuccess(
                success.Data),
            OperationResponse<T, U>.FailedOperation failed => await onError(
                failed.Data),
            _ => throw new InvalidOperationException("Unknown response type")
        };
    }
    
    
    public static async Task<TResult> Match<T, U, TResult>(
        this OperationResponse<T, U> responseTask,
        Func<T, Task<TResult>> onSuccess,
        Func<U, Task<TResult>> onError)
    {
        return responseTask switch
        {
            OperationResponse<T, U>.SuccessfulOperation success => await onSuccess(
                success.Data),
            OperationResponse<T, U>.FailedOperation failed => await onError(
                failed.Data),
            _ => throw new InvalidOperationException("Unknown response type")
        };
    }
    
    public static TResult Match<T, U, TResult>(
        this OperationResponse<T, U> responseTask,
        Func<T, TResult> onSuccess,
        Func<U, TResult> onError)
    {
        return responseTask switch
        {
            OperationResponse<T, U>.SuccessfulOperation success => onSuccess(
                success.Data),
            OperationResponse<T, U>.FailedOperation failed => onError(
                failed.Data),
            _ => throw new InvalidOperationException("Unknown response type")
        };
    }
    
    public static async Task<TResult> Match<T, U, TResult>(
        this Task<OperationResponse<T, U>> responseTask,
        Func<T, TResult> onSuccess,
        Func<U, TResult> onError)
    {
        var response = await responseTask;

        return response switch
        {
            OperationResponse<T, U>.SuccessfulOperation success => onSuccess(
                success.Data),
            OperationResponse<T, U>.FailedOperation failed => onError(
                failed.Data),
            _ => throw new InvalidOperationException("Unknown response type")
        };
    }


    public static async Task<TResult> Match<T, U, TResult>(
        this Task<OperationResponse<T, U>> responseTask,
        Func<T, Task<TResult>> onSuccess,
        Func<U, Task<TResult>> onError)
    {
        var response = await responseTask;

        return response switch
        {
            OperationResponse<T, U>.SuccessfulOperation success => await onSuccess(
                success.Data),
            OperationResponse<T, U>.FailedOperation failed => await onError(
                failed.Data),
            _ => throw new InvalidOperationException("Unknown response type")
        };
    }


    public static async Task<OperationResponse<T, TU>> FallbackTo<T, TU>(
        this Task<OperationResponse<T, TU>> resultTask,
        Func<Task<OperationResponse<T, TU>>> fallbackFunc)
    {
        var result = await resultTask;

        if (result is OperationResponse<T, TU>.FailedOperation)
            return await fallbackFunc();

        return result;
    }


    // This method allows you to run a side effect after the main operation completes successfully.
    // It will always return the original result, regardless of the side effect's outcome.
    public static async Task<OperationResponse<T, TU>> RunSideEffect<T, TU>(
        this Task<OperationResponse<T, TU>> resultTask,
        Func<T, Task> sideEffectFunc)
    {
        var result = await resultTask;

        if (result is OperationResponse<T, TU>.SuccessfulOperation successfulOperation)
            // Execute the side effect but ignore its result
            await sideEffectFunc(successfulOperation.Data);

        // Return the original result regardless of the side effect's outcome
        return result;
    }
    
}

