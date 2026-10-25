namespace RenTrek.BLL.Helpers;

public interface IParticipantInviteTokenService
{
    string CreateToken();
    string HashToken(string token);
}
