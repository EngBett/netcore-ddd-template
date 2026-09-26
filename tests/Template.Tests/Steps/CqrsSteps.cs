using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Template.Application.Features.Todos.Commands;
using Template.Application.Features.Todos.Models;
using Template.Application.Features.Todos.Queries;
using Template.Common.Models;
using Template.Tests.Support;
using Wolverine;
using Xunit;

namespace Template.Tests.Steps;

[Binding]
public class CqrsSteps
{
    private object? _response;
    private ValidationException? _validationFailure;
    private long _elapsedMilliseconds;

    [When("I dispatch a todo query for user {string}")]
    public async Task WhenIDispatchATodoQueryForUser(string userId) =>
        await DispatchAsync<ApiResponse<IEnumerable<TodoDto>>>(new GetTodosQuery { UserId = userId });

    [When("I dispatch a create todo command titled {string} described as {string}")]
    public async Task WhenIDispatchACreateTodoCommand(string title, string description) =>
        await DispatchAsync<ApiResponse<TodoDto>>(
            new CreateTodoCommand { Title = title, Description = description });

    private async Task DispatchAsync<TResponse>(object message)
    {
        using var scope = TestHost.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _response = await bus.InvokeAsync<TResponse>(message);
        }
        catch (ValidationException exception)
        {
            _validationFailure = exception;
        }
        finally
        {
            stopwatch.Stop();
            _elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        }
    }

    [Then("the dispatch succeeds")]
    public void ThenTheDispatchSucceeds()
    {
        Assert.Null(_validationFailure);
        Assert.NotNull(_response);
    }

    [Then("the dispatch is rejected as invalid")]
    public void ThenTheDispatchIsRejectedAsInvalid()
    {
        Assert.NotNull(_validationFailure);
        Assert.Null(_response);
    }

    [Then("the validation failures mention {string}")]
    public void ThenTheValidationFailuresMention(string field)
    {
        Assert.NotNull(_validationFailure);
        Assert.Contains(
            _validationFailure!.Errors,
            failure => failure.ErrorMessage.Contains(field, StringComparison.OrdinalIgnoreCase));
    }

    [Then("there are exactly {int} validation failures")]
    public void ThenThereAreExactlyValidationFailures(int expected)
    {
        Assert.NotNull(_validationFailure);
        Assert.Equal(expected, _validationFailure!.Errors.Count());
    }

    [Then("the rejection took less than {int} milliseconds")]
    public void ThenTheRejectionTookLessThanMilliseconds(int budget)
    {
        // Guards the retry scoping: a global Wolverine failure policy would also cover
        // InvokeAsync, and the configured cooldowns would push a rejected command past this
        // budget before the caller ever saw the error.
        Assert.True(
            _elapsedMilliseconds < budget,
            $"Expected the rejection inside {budget}ms but it took {_elapsedMilliseconds}ms, "
            + "which suggests retry policies are being applied to in-process dispatch.");
    }
}
