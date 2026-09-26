namespace Template.Application.Interfaces
{
    public interface ICurrentUserService
    {
        string? UserId { get; }
        string? Email { get; }
        string? PhoneNumber { get; }
    }
}
