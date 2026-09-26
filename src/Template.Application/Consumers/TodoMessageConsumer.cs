using Microsoft.Extensions.Logging;
using Template.Common.Messages.Todos;

namespace Template.Application.Consumers;

// Wolverine has no IConsumer<T> to implement: a class whose name ends in Consumer (or
// Handler) with a Consume/Handle method is discovered by convention, and the message
// type is taken from the first parameter. Dependencies are injected into the method or
// the constructor.
public class TodoMessageConsumer(ILogger<TodoMessageConsumer> logger)
{
    public async Task Consume(TodoMessage message)
    {
        logger.LogInformation("Received TodoMessage: {Message}", message);
        await Task.CompletedTask;
    }
}
