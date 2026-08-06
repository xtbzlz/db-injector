// TbcCli.cs — test client for the tb_bridge named pipe protocol (P/Invoke).
// Usage: TbcCli.exe <code> [code...] | PING
using System;
using System.Text;
using System.Runtime.InteropServices;

class TbcCli
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sa, uint disp, uint flags, IntPtr tpl);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(IntPtr h, byte[] buf, uint n, out uint read, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteFile(IntPtr h, byte[] buf, uint n, out uint written, IntPtr ov);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr h);

    const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2;
    const uint OPEN_EXISTING = 3;

    static IntPtr Connect()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            IntPtr h = CreateFileW(@"\\.\pipe\tbc_bridge", GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h.ToInt64() != -1) return h;
            System.Threading.Thread.Sleep(50);
        }
        throw new Exception("cannot open pipe (err " + Marshal.GetLastWin32Error() + ")");
    }

    static uint ReadExact(IntPtr h, byte[] b, int n)
    {
        int got = 0;
        while (got < n)
        {
            uint r;
            if (!ReadFile(h, b, (uint)(n - got), out r, IntPtr.Zero) || r == 0)
                throw new Exception("pipe closed");
            got += (int)r;
        }
        return (uint)got;
    }

    static string ReadLine(IntPtr h)
    {
        var sb = new StringBuilder();
        byte[] one = new byte[1];
        while (true)
        {
            uint r;
            if (!ReadFile(h, one, 1, out r, IntPtr.Zero) || r == 0) break;
            if (one[0] == (byte)'\n') break;
            sb.Append((char)one[0]);
        }
        return sb.ToString();
    }

    static string Eval(IntPtr h, string code, bool scriptMode)
    {
        byte[] codeBytes = Encoding.UTF8.GetBytes(code);
        byte[] head = Encoding.ASCII.GetBytes(scriptMode ? "EVALS\n" : "EVAL\n");
        byte[] len = BitConverter.GetBytes(codeBytes.Length);
        uint w;
        WriteFile(h, head, (uint)head.Length, out w, IntPtr.Zero);
        WriteFile(h, len, 4, out w, IntPtr.Zero);
        WriteFile(h, codeBytes, (uint)codeBytes.Length, out w, IntPtr.Zero);

        string status = ReadLine(h);
        if (status == "PONG") return "PONG";
        byte[] lenB = new byte[4];
        ReadExact(h, lenB, 4);
        int payloadLen = BitConverter.ToInt32(lenB, 0);
        byte[] payload = new byte[payloadLen];
        ReadExact(h, payload, payloadLen);
        string body = Encoding.UTF8.GetString(payload);
        return status == "OK" ? body : "ERR: " + body;
    }

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: TbcCli.exe <code> [code...] | PING | EVALS <code>");
            return 1;
        }
        try
        {
            bool scriptMode = false;
            int start = 0;
            if (args[0] == "EVALS")
            {
                scriptMode = true;
                start = 1;
                if (args.Length == 1) { Console.Error.WriteLine("usage: EVALS <code>"); return 1; }
            }
            if (args[0] == "PING")
            {
                IntPtr h = Connect();
                try
                {
                    byte[] p = Encoding.ASCII.GetBytes("PING\n");
                    uint w;
                    WriteFile(h, p, (uint)p.Length, out w, IntPtr.Zero);
                    Console.WriteLine(ReadLine(h));
                }
                finally { CloseHandle(h); }
                return 0;
            }
            if (args[0] == "LIST")
            {
                IntPtr h = Connect();
                try
                {
                    byte[] p = Encoding.ASCII.GetBytes("LIST\n");
                    uint w;
                    WriteFile(h, p, (uint)p.Length, out w, IntPtr.Zero);
                    string status = ReadLine(h);
                    byte[] lenB = new byte[4];
                    ReadExact(h, lenB, 4);
                    int payloadLen = BitConverter.ToInt32(lenB, 0);
                    byte[] payload = new byte[payloadLen];
                    ReadExact(h, payload, payloadLen);
                    string body = Encoding.UTF8.GetString(payload);
                    Console.WriteLine(status == "OK" ? body : "ERR: " + body);
                }
                finally { CloseHandle(h); }
                return 0;
            }
            for (int i = start; i < args.Length; i++)
            {
                string a = args[i];
                Console.WriteLine("> " + a);
                IntPtr s = Connect();
                try
                {
                    Console.WriteLine("= " + Eval(s, a, scriptMode));
                }
                finally
                {
                    CloseHandle(s);
                }
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 2;
        }
    }
}
