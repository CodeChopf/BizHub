using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IUserRepository
{
    bool HasAnyUser();
    List<User> GetAll();
    User? GetByUsername(string username);
    User Create(string username, string password, bool isAdmin, bool isPlatformAdmin = false);
    User CreateWithHash(string username, string passwordHash, bool isAdmin, bool isPlatformAdmin = false);
    void Delete(string username);
    void ChangePassword(string username, string newPassword);
}
