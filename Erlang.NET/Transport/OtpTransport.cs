using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
    public static class OtpTransport
    {
        public static void Close(this IOtpTransport s)
        {
            if (s is null)
                return;
            try { s.Close(); }
            catch (Exception) { }
        }

        public static void Close(this TcpClient s)
        {
            if (s is null)
                return;
            try { s.GetStream().Close(); }
            catch (Exception) { }
            try { s.Close(); }
            catch (Exception) { }
        }

        public static void Close(this IOtpServerTransport s)
        {
            if (s is null)
                return;
            try { s.Close(); }
            catch (Exception) { }
        }

        public static void Close(this TcpListener s)
        {
            if (s is null)
                return;
            try { s.Stop(); }
            catch (Exception) { }
        }

        public static async Task<OtpInputStream> ReadAsync(this IOtpTransport s, int bytes)
        {
            if (s is null)
                return default;

            byte[] data = new byte[bytes];
            int offset = 0;
            int read;

            while (offset < data.Length)
            {
                read = await s.InputStream.ReadAsync(data, offset, bytes - offset);
                if (read == 0)
                    throw new IOException($"Read failed; expected {bytes} bytes, got {offset} bytes");
                offset += read;
            }

            return new OtpInputStream(data);
        }
    }
}
