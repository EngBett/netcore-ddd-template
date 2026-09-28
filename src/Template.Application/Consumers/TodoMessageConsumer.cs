using Microsoft.Extensions.Logging;
using Template.Common.Messages.Todos;

namespace Template.Application.Consumers;

// Wolverine has no IConsumer<T> to implement: a class whose name ends in Consumer (or
// Handler) with a Consume/Handle method is discovered by convention, and the message
// type is taken from the first parameter. Dependencies are injected into the method or
// the constructor.
public partial class TodoMessageConsumer(ILogger<TodoMessageConsumer> logger)
{
    public async Task Consume(TodoMessage message)
    {
        LogReceived(logger, message.Text);
        await Task.CompletedTask;
    }

    // Source-generated, so nothing is formatted or boxed when Information is switched off.
    [LoggerMessage(Level = LogLevel.Information, Message = "Received TodoMessage: {Text}")]
    private static partial void LogReceived(ILogger logger, string text);
}
