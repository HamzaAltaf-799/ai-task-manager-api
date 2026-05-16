using AITaskManager.API.Data;
using AITaskManager.API.Interfaces;
using AITaskManager.API.Models;

namespace AITaskManager.API.Repositories;

public class UserRepository : IUserRepository
{
    private readonly InMemoryDatabase _db;

    public UserRepository(InMemoryDatabase db) => _db = db;

    public Task<ApplicationUser?> GetByIdAsync(string id)
    {
        var user = _db.Users.Values.FirstOrDefault(u => u.Id == id);
        return Task.FromResult(user);
    }

    public Task<ApplicationUser?> GetByEmailAsync(string email)
    {
        _db.Users.TryGetValue(email.ToLowerInvariant(), out var user);
        return Task.FromResult(user);
    }

    public Task<ApplicationUser> CreateAsync(ApplicationUser user)
    {
        _db.Users[user.Email.ToLowerInvariant()] = user;
        return Task.FromResult(user);
    }

    public Task<ApplicationUser> UpdateAsync(ApplicationUser user)
    {
        _db.Users[user.Email.ToLowerInvariant()] = user;
        return Task.FromResult(user);
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        return Task.FromResult(_db.Users.ContainsKey(email.ToLowerInvariant()));
    }
}
