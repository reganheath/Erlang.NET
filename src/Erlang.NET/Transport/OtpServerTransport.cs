namespace Erlang.NET
{
    public interface OtpServerTransport
    {
        void Start();
        int GetLocalPort();
        OtpTransport Accept();
        void Close();
    }
}
