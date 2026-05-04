using FluentValidation;
using Template.Application.Features.Todos.Queries;

namespace Template.Application.Features.Todos.Validators;

public class GetTodosQueryValidator : AbstractValidator<GetTodosQuery>
{
    public GetTodosQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}