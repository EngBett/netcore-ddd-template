using System.Collections.Concurrent;
using FluentValidation;
using FluentValidation.Results;
using Template.Application.Features.Todos.Models;
using Template.Application.Features.Todos.Queries;
using Template.Common.Models;
using Template.Domain.Exceptions;

namespace Template.Tests.Support;

/// <summary>An outcome a scenario asks the sample query to produce.</summary>
public enum QueryFault
{
    ValidationFailure,
    DomainRuleViolation,
    NotFound,
    UnexpectedException
}

/// <summary>
/// Makes the sample <see cref="GetTodosQuery"/> fail in a chosen way, so the HTTP scenarios can
/// check how each outcome reaches the client without the service shipping an endpoint that
/// fails on purpose.
/// </summary>
/// <remarks>
/// Faults are keyed by <see cref="GetTodosQuery.UserId"/>, which each scenario picks uniquely,
/// so scenarios running in parallel cannot see each other's faults.
/// </remarks>
public static class QueryFaults
{
    private static readonly ConcurrentDictionary<string, (QueryFault Fault, string Message)> Faults = new();

    public static void Arrange(string userId, QueryFault fault, string message) =>
        Faults[userId] = (fault, message);

    public static void Clear(string userId) => Faults.TryRemove(userId, out _);

    /// <summary>
    /// Wolverine middleware wrapped around the <see cref="GetTodosQuery"/> handler by
    /// <see cref="ApiHost"/>. <c>Before</c> runs ahead of the handler, <c>After</c> receives
    /// the response it returned.
    /// </summary>
    public static class Middleware
    {
        public static void Before(GetTodosQuery query)
        {
            if (!Faults.TryGetValue(query.UserId, out var arranged))
                return;

            switch (arranged.Fault)
            {
                case QueryFault.ValidationFailure:
                    throw new ValidationException([new ValidationFailure(nameof(GetTodosQuery.UserId), arranged.Message)]);
                case QueryFault.DomainRuleViolation:
                    throw new DomainException(arranged.Message);
                case QueryFault.UnexpectedException:
                    throw new InvalidOperationException(arranged.Message);
            }
        }

        public static void After(GetTodosQuery query, ApiResponse<IEnumerable<TodoDto>> response)
        {
            if (Faults.TryGetValue(query.UserId, out var arranged) && arranged.Fault == QueryFault.NotFound)
            {
                response.Code = ResponseEnums.ResponseCodes.NotFound;
                response.Message = arranged.Message;
            }
        }
    }
}
