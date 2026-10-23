using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.BLL.Services.Implementations;

public class PasswordHasher : IPasswordHasher
{
    // TODO: dung BCrypt.Net.BCrypt (package BCrypt.Net-Next da duoc them)
    public string Hash(string password) => throw new NotImplementedException();

    public bool Verify(string password, string passwordHash) => throw new NotImplementedException();
}
