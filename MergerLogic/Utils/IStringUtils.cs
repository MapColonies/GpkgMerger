namespace MergerLogic.Utils
{
    public interface IStringUtils
    {
        string ByteArrayToString(byte[] ba);
        byte[] StringToByteArray(string hex);
    }
}