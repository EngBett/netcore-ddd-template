using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Template.Application.Features.Todos.Commands;
using Template.Application.Features.Todos.Queries;
using Template.Common.Messages.Todos;
using Template.Domain.DomainEvents.Todos;
using Template.Tests.Support;
using Wolverine;
using Xunit;

namespace Template.Tests.Steps;

[Binding]
public class BrokerRoutingSteps
{
    private IReadOnlyList<string> _destinations = [];

    [When("I inspect the routing for a {word} {word} {word}")]
    public void WhenIInspectTheRoutingForAThreeWordMessage(string first, string second, string third) =>
        Inspect($"{first} {second} {third}");

    [When("I inspect the routing for a {word} {word}")]
    public void WhenIInspectTheRoutingForATwoWordMessage(string first, string second) =>
        Inspect($"{first} {second}");

    private void Inspect(string description)
    {
        object message = description switch
        {
            "create todo command" => new CreateTodoCommand(),
            "todo query" => new GetTodosQuery(),
            "todo created event" => new TodoCreatedEvent(),
            "todo message" => new TodoMessage(),
            _ => throw new NotSupportedException($"Unknown message '{description}'.")
        };

        using var scope = TestHost.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        // PreviewSubscriptions reports where a message would go without sending it, so the
        // routing can be asserted with no broker running.
        _destinations = bus.PreviewSubscriptions(message)
            .Select(envelope => envelope.Destination?.Scheme ?? "none")
            .ToList();
    }

    [Then("it is routed to the broker")]
    public void ThenItIsRoutedToTheBroker() =>
        Assert.Contains("rabbitmq", _destinations);

    [Then("it is not routed to the broker")]
    public void ThenItIsNotRoutedToTheBroker() =>
        Assert.DoesNotContain("rabbitmq", _destinations);

    [Then("it is routed in process")]
    public void ThenItIsRoutedInProcess() =>
        Assert.Contains("local", _destinations);
}
