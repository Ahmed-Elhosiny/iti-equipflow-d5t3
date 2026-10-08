namespace EquipFlow.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public User(Guid id, string username, string passwordHash, string role, bool isActive = true)
    {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = isActive;
    }
}
