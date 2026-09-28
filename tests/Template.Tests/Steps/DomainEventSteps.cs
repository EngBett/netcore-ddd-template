using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Template.Domain.DomainEvents.Todos;
using Template.Tests.Support;
using Xunit;

namespace Template.Tests.Steps;

[Binding]
public class DomainEventSteps
{
    private Widget? _widget;
    private bool _saved;

    [BeforeScenario]
    public static void ResetSpies() => SpyTodoCreatedHandler.Reset();

    [Given("a widget that raises a todo created event")]
    public void GivenAWidgetThatRaisesATodoCreatedEvent()
    {
        _widget = new Widget();
        _widget.AddDomainEvent(new TodoCreatedEvent());
    }

    [Given("a widget that raises an event nothing handles")]
    public void GivenAWidgetThatRaisesAnEventNothingHandles()
    {
        _widget = new Widget();
        _widget.AddDomainEvent(new UnhandledTestEvent());
    }

    [Given("a widget that raises no events")]
    public void GivenAWidgetThatRaisesNoEvents() => _widget = new Widget();

    [When("I save changes")]
    public async Task WhenISaveChanges()
    {
        using var scope = TestHost.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestApplicationContext>();

        context.Widgets.Add(_widget!);
        await context.SaveChangesAsync();
        _saved = true;
    }

    [Then("the todo created event handler ran once")]
    public static void ThenTheTodoCreatedEventHandlerRanOnce() =>
        Assert.Equal(1, SpyTodoCreatedHandler.Invocations);

    [Then("the todo created event handler did not run")]
    public static void ThenTheTodoCreatedEventHandlerDidNotRun() =>
        Assert.Equal(0, SpyTodoCreatedHandler.Invocations);

    [Then("the save succeeds")]
    public void ThenTheSaveSucceeds() =>
        // An event nobody handles is unroutable, and letting that surface would fail the
        // whole write rather than just skipping a side effect.
        Assert.True(_saved, "SaveChangesAsync did not complete.");
}
