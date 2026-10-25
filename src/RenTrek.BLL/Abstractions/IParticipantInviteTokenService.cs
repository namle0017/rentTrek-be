namespace RenTrek.BLL.Abstractions;

public interface IParticipantInviteTokenService
{
    string CreateToken();
    string HashToken(string token);
}
