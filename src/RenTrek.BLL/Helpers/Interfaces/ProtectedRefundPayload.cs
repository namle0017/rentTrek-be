namespace RenTrek.BLL.Helpers.Interfaces;

public sealed record ProtectedRefundPayload(byte[] Ciphertext, byte[] Nonce, byte[] Tag);
