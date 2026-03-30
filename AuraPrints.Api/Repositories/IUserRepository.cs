using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IUserRepository
{
    bool HasAnyUser();
    List<User> GetAll();
    User? GetByUsername(string username);
    User Create(string username, string password, bool isAdmin);
    User CreateWithHash(string username, string passwordHash, bool isAdmin);
    void Delete(string username);
    void ChangePassword(string username, string newPassword);
}
