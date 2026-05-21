namespace SupRoomInfoIngestion.Contracts;

public interface IBedInfoRestoreStep
{
    string Restore(string? bedInfoKey);
}
