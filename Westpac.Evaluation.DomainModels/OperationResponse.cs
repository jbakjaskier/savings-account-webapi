
using System.Diagnostics.CodeAnalysis;

namespace Westpac.Evaluation.DomainModels;


/// <summary>
/// This acts as a Result object and returns a relavant success model when the operation is successful or failure when it's failed
/// This 
/// </summary>
public abstract record OperationResponse<TSuccess, TFailure>
{
    
    private OperationResponse()
    {
        
    }

    [method: SetsRequiredMembers]
    public sealed record SuccessfulOperation(TSuccess Data) : OperationResponse<TSuccess, TFailure>
    {
        public required TSuccess Data { get; init; } = Data;
    }

    [method: SetsRequiredMembers]
    public sealed record FailedOperation(TFailure Data) : OperationResponse<TSuccess, TFailure>
    {
        public required TFailure Data = Data;
    }
    
}