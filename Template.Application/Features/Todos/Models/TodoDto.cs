namespace Template.Application.Features.Todos.Models;

public class TodoDto
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public bool IsCompleted { get; set; }
}