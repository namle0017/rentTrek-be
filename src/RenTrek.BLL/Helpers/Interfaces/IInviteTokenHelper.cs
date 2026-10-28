namespace RenTrek.BLL.Helpers.Interfaces;

public interface IInviteTokenHelper
{
    string CreateToken();
    string HashToken(string token);
}
