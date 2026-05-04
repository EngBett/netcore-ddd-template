using MassTransit;
using Microsoft.Extensions.Logging;
using Template.Common.Messages.Todos;

namespace Template.Application.Consumers;

public class TodoMessageConsumer(ILogger<TodoMessageConsumer> logger) : IConsumer<TodoMessage>
{
    public async Task Consume(ConsumeContext<TodoMessage> context)
    {
        logger.LogInformation("Received TodoMessage: {Message}", context.Message);
        await Task.CompletedTask;
    }
}