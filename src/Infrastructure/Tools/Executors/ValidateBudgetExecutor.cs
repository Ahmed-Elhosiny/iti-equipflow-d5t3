using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EquipFlow.Application.Agentic.Abstractions;
using EquipFlow.Application.Tools.Definitions;
using EquipFlow.Application.Tools.Ports;

namespace EquipFlow.Infrastructure.Tools.Executors;

/// <summary>
/// Executes the <c>validate_budget</c> tool.
/// </summary>
/// <param name="costGovernor">The Cost Governor used to validate the estimated cost.</param>
public sealed class ValidateBudgetExecutor(ICostGovernor costGovernor) : IToolExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the name of the tool handled by this executor.
    /// </summary>
    public string ToolName => "validate_budget";

    /// <summary>
    /// Deserializes the tool arguments and validates the estimated cost with the Cost Governor.
    /// </summary>
    /// <param name="arguments">The JSON arguments supplied to the tool.</param>
    /// <param name="cancellationToken">The token used to cancel execution.</param>
    /// <returns>A successful result containing the budget validation, or a failure result.</returns>
    public Task<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(arguments, null, cancellationToken);

    private async Task<ToolExecutionResult> ExecuteAsync(
        JsonElement arguments,
        string? userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = arguments.Deserialize<ValidateBudgetRequest>(SerializerOptions);
            if (request is null)
            {
                return new ToolExecutionResult(
                    false,
                    null,
                    "The validate_budget arguments could not be deserialized.");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return new ToolExecutionResult(
                    false,
                    null,
                    "The validate_budget invocation is missing a user context.");
            }

            var budgetCheck = await costGovernor.CheckBudgetAsync(
                userId,
                new EstimatedCost(request.EstimatedCost, 0, 0),
                cancellationToken);

            return new ToolExecutionResult(
                true,
                JsonSerializer.Serialize(new
                {
                    IsApproved = budgetCheck.IsAllowed,
                    EstimatedCost = request.EstimatedCost,
                    RejectionReason = budgetCheck.Reason
                }),
                null);
        }
        catch (JsonException exception)
        {
            return new ToolExecutionResult(
                false,
                null,
                $"The validate_budget arguments are invalid: {exception.Message}");
        }
        catch (ValidationException exception)
        {
            return new ToolExecutionResult(false, null, exception.Message);
        }
        catch (Exception exception)
        {
            return new ToolExecutionResult(false, null, exception.Message);
        }
    }

    /// <inheritdoc />
    async Task<ToolDispatchResult> IToolExecutor.ExecuteAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using var document = JsonDocument.Parse(request.ArgumentsJson);
        var result = await ExecuteAsync(
            document.RootElement,
            request.Context.UserId.ToString(),
            cancellationToken);

        return new ToolDispatchResult(
            request.ToolName,
            result.Succeeded,
            result.Succeeded ? ToolDispatchStatus.Success : ToolDispatchStatus.ExecutorFailed,
            result.Result,
            result.Succeeded ? null : "VALIDATE_BUDGET_FAILED",
            result.Error);
    }
}