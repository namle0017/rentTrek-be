namespace RenTrek.BLL.Helpers.Interfaces;

public interface IInviteTokenHelper
{
    /// <summary>Mã 6 chữ số ngẫu nhiên (có thể bắt đầu bằng 0).</summary>
    string CreateCode();
    string HashToken(string token);
}
