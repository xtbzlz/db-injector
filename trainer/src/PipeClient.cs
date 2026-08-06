// PipeClient.cs — named pipe client for the tb_bridge protocol.
// The server closes the connection after each command, so every Eval opens a
// fresh connection (with retry) and drops the handle afterwards.
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace DzbTrainer
{
    public class PipeClient
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sa, uint disp, uint flags, IntPtr tpl);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool WaitNamedPipe(string name, int timeout);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadFile(IntPtr h, byte[] buf, uint n, out uint read, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(IntPtr h, byte[] buf, uint n, out uint written, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool PeekNamedPipe(IntPtr h, byte[] buf, uint n, out uint read, out uint avail, out uint msgLeft);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2;
        const uint OPEN_EXISTING = 3;

        IntPtr hPipe = IntPtr.Zero;
        readonly object sync = new object();

        // 调试回调：连接失败/重试明细（由 Trainer 在调试模式时挂接，写入 debug.log）
        public static Action<string> DebugLog;

        static void Dbg(string msg)
        {
            if (DebugLog != null) { try { DebugLog(msg); } catch { } }
        }

        public bool Connected { get { return hPipe.ToInt64() != 0 && hPipe.ToInt64() != -1; } }

        public bool Connect()
        {
            // 先 WaitNamedPipe 预检避免 CreateFileW 在实例全忙时无限阻塞；
            // 插件在 Disconnect→Create 间隙会短暂无实例，故预检加重试覆盖
            bool waited = false;
            for (int w = 0; w < 5 && !waited; w++)
            {
                if (WaitNamedPipe(@"\\.\pipe\tbc_bridge", 500)) waited = true;
                else Thread.Sleep(50);
            }
            if (!waited) return false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                IntPtr h = CreateFileW(@"\\.\pipe\tbc_bridge", GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (h.ToInt64() != -1)
                {
                    hPipe = h;
                    return true;
                }
                int err = Marshal.GetLastWin32Error();
                if (attempt == 0) Dbg("pipe connect fail err=" + err + " (game not running or plugin not loaded)");
                Thread.Sleep(50);
            }
            return false;
        }

        public void Close()
        {
            if (Connected) { CloseHandle(hPipe); hPipe = IntPtr.Zero; }
        }

        public string Ping()
        {
            lock (sync)
            {
                if (!Connect()) return "ERR: cannot connect";
                try
                {
                    byte[] p = Encoding.ASCII.GetBytes("PING\n");
                    uint w;
                    if (!WriteFile(hPipe, p, (uint)p.Length, out w, IntPtr.Zero)) return "ERR: write failed";
                    return ReadLine();
                }
                finally { Close(); }
            }
        }

        public string Eval(string code)
        {
            lock (sync)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (!Connect()) return "ERR: cannot connect";
                    try
                    {
                        string r = EvalRaw("EVAL\n", code);
                        // server closes the connection after each command
                        Close();
                        if (r.StartsWith("ERR: write failed") || r.StartsWith("ERR: pipe closed") || r.StartsWith("ERR: cannot open"))
                        {
                            Close();
                            continue; // transient: reconnect and retry
                        }
                        return r;
                    }
                    catch
                    {
                        Close();
                    }
                }
                return "ERR: connection lost";
            }
        }

        public string EvalScript(string code)
        {
            lock (sync)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (!Connect()) return "ERR: cannot connect";
                    try
                    {
                        string r = EvalRaw("EVALS\n", code);
                        Close();
                        if (r.StartsWith("ERR: write failed") || r.StartsWith("ERR: pipe closed") || r.StartsWith("ERR: cannot open"))
                        {
                            Close();
                            continue;
                        }
                        return r;
                    }
                    catch { Close(); }
                }
                return "ERR: connection lost";
            }
        }

        public string List()
        {
            lock (sync)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (!Connect()) return "ERR: cannot connect";
                    try
                    {
                        byte[] p = Encoding.ASCII.GetBytes("LIST\n");
                        uint w;
                        if (!WriteFile(hPipe, p, (uint)p.Length, out w, IntPtr.Zero)) return "ERR: write failed";
                        string status = ReadLine();
                        if (status == "ERR") return "ERR: list failed";
                        byte[] lenB = new byte[4];
                        if (!ReadExact(lenB, 4)) return "ERR: pipe closed";
                        int payloadLen = BitConverter.ToInt32(lenB, 0);
                        if (payloadLen < 0 || payloadLen > 8 * 1024 * 1024) return "ERR: bad payload len";
                        byte[] payload = new byte[payloadLen];
                        if (!ReadExact(payload, payloadLen)) return "ERR: pipe closed";
                        Close();
                        return Encoding.UTF8.GetString(payload);
                    }
                    catch { Close(); }
                }
                return "ERR: connection lost";
            }
        }

        string EvalRaw(string cmd, string code)
        {
            byte[] codeBytes = Encoding.UTF8.GetBytes(code);
            byte[] head = Encoding.ASCII.GetBytes(cmd);
            byte[] len = BitConverter.GetBytes(codeBytes.Length);
            uint w;
            if (!WriteFile(hPipe, head, (uint)head.Length, out w, IntPtr.Zero)) return "ERR: write failed";
            if (!WriteFile(hPipe, len, 4, out w, IntPtr.Zero)) return "ERR: write failed";
            if (!WriteFile(hPipe, codeBytes, (uint)codeBytes.Length, out w, IntPtr.Zero)) return "ERR: write failed";

            string status = ReadLine();
            if (status == "PONG") return "PONG";
            byte[] lenB = new byte[4];
            if (!ReadExact(lenB, 4)) return "ERR: pipe closed";
            int payloadLen = BitConverter.ToInt32(lenB, 0);
            if (payloadLen < 0 || payloadLen > 4 * 1024 * 1024) return "ERR: bad payload len";
            byte[] payload = new byte[payloadLen];
            if (!ReadExact(payload, payloadLen)) return "ERR: pipe closed";
            string body = Encoding.UTF8.GetString(payload);
            return status == "OK" ? body : "ERR: " + body;
        }

        bool ReadExact(byte[] b, int n)
        {
            int got = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (got < n)
            {
                uint r, avail, m;
                if (!PeekNamedPipe(hPipe, null, 0, out r, out avail, out m)) return false;
                if (avail == 0)
                {
                    if (sw.ElapsedMilliseconds > ReadTimeoutMs) return false;
                    Thread.Sleep(20);
                    continue;
                }
                if (!ReadFile(hPipe, b, (uint)(n - got), out r, IntPtr.Zero) || r == 0)
                    return false;
                got += (int)r;
            }
            return true;
        }

        string ReadLine()
        {
            var sb = new StringBuilder();
            byte[] one = new byte[1];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                uint r, avail, m;
                if (!PeekNamedPipe(hPipe, null, 0, out r, out avail, out m)) break;
                if (avail == 0)
                {
                    if (sw.ElapsedMilliseconds > ReadTimeoutMs) { Dbg("read timeout"); break; }
                    Thread.Sleep(20);
                    continue;
                }
                if (!ReadFile(hPipe, one, 1, out r, IntPtr.Zero) || r == 0) break;
                if (one[0] == (byte)'\n') break;
                sb.Append((char)one[0]);
            }
            return sb.ToString();
        }

        const int ReadTimeoutMs = 5000;
    }
}
