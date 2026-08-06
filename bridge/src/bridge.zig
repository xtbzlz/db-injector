// tb_bridge.zig
// krkrz (KiriKiri Z) plugin bridge for the D&B trainer.
// Loaded by the game engine via V2Link; hosts a named-pipe server that
// executes TJS code in the game process on the main thread.
// Target: x86-windows-gnu

const std = @import("std");
const alloc = std.heap.c_allocator;

// ---------------------------------------------------------------------------
// Windows primitives
// ---------------------------------------------------------------------------
const HANDLE = *anyopaque;
const HWND = *anyopaque;
const WPARAM = usize;
const LPARAM = isize;
const DWORD = u32;
const BOOL = i32;
const LPVOID = ?*anyopaque;

const STD_CALL = std.builtin.CallingConvention.winapi;

extern "kernel32" fn CreateNamedPipeW(
    lpName: [*:0]const u16,
    dwOpenMode: u32,
    dwPipeMode: u32,
    nMaxInstances: u32,
    nOutBufferSize: u32,
    nInBufferSize: u32,
    nDefaultTimeOut: u32,
    lpSecurityAttributes: ?*anyopaque,
) callconv(STD_CALL) ?HANDLE;
extern "kernel32" fn ConnectNamedPipe(hNamedPipe: HANDLE, lpOverlapped: ?*anyopaque) callconv(STD_CALL) BOOL;
extern "kernel32" fn DisconnectNamedPipe(hNamedPipe: HANDLE) callconv(STD_CALL) BOOL;
extern "kernel32" fn ReadFile(
    hFile: HANDLE,
    lpBuffer: [*]u8,
    nNumberOfBytesToRead: u32,
    lpNumberOfBytesRead: *u32,
    lpOverlapped: ?*anyopaque,
) callconv(STD_CALL) BOOL;
extern "kernel32" fn WriteFile(
    hFile: HANDLE,
    lpBuffer: [*]const u8,
    nNumberOfBytesToWrite: u32,
    lpNumberOfBytesWritten: *u32,
    lpOverlapped: ?*anyopaque,
) callconv(STD_CALL) BOOL;
extern "kernel32" fn CreateEventW(
    lpEventAttributes: ?*anyopaque,
    bManualReset: BOOL,
    bInitialState: BOOL,
    lpName: ?[*:0]const u16,
) callconv(STD_CALL) ?HANDLE;
extern "kernel32" fn SetEvent(hEvent: HANDLE) callconv(STD_CALL) BOOL;
extern "kernel32" fn WaitForSingleObject(hHandle: HANDLE, dwMilliseconds: u32) callconv(STD_CALL) u32;
extern "kernel32" fn CloseHandle(hObject: HANDLE) callconv(STD_CALL) BOOL;
extern "kernel32" fn CreateThread(
    lpThreadAttributes: ?*anyopaque,
    dwStackSize: usize,
    lpStartAddress: *const anyopaque,
    lpParameter: ?*anyopaque,
    dwCreationFlags: u32,
    lpThreadId: ?*u32,
) callconv(STD_CALL) ?HANDLE;
extern "kernel32" fn GetModuleFileNameW(
    hModule: ?HANDLE,
    lpFilename: [*]u16,
    nSize: u32,
) callconv(STD_CALL) u32;
extern "kernel32" fn GetLastError() callconv(STD_CALL) u32;
extern "kernel32" fn GetCurrentProcessId() callconv(STD_CALL) u32;
extern "kernel32" fn Sleep(dwMilliseconds: u32) callconv(STD_CALL) void;

fn sleepMs(ms: u32) void {
    Sleep(ms);
}

extern "user32" fn RegisterClassW(lpWndClass: *const WNDCLASSW) callconv(STD_CALL) u16;
extern "user32" fn CreateWindowExW(
    dwExStyle: u32,
    lpClassName: [*:0]const u16,
    lpWindowName: [*:0]const u16,
    dwStyle: u32,
    x: i32,
    y: i32,
    nWidth: i32,
    nHeight: i32,
    hWndParent: ?HWND,
    hMenu: ?HANDLE,
    hInstance: ?HANDLE,
    lpParam: ?*anyopaque,
) callconv(STD_CALL) ?HWND;
extern "user32" fn PostMessageW(
    hWnd: ?HWND,
    msg: u32,
    wParam: WPARAM,
    lParam: LPARAM,
) callconv(STD_CALL) BOOL;
extern "user32" fn DefWindowProcW(
    hWnd: HWND,
    msg: u32,
    wParam: WPARAM,
    lParam: LPARAM,
) callconv(STD_CALL) LPARAM;
extern "user32" fn DestroyWindow(hWnd: HWND) callconv(STD_CALL) BOOL;
extern "kernel32" fn FreeLibrary(hLibModule: HANDLE) callconv(STD_CALL) BOOL;

const WNDCLASSW = extern struct {
    style: u32,
    lpfnWndProc: ?*const anyopaque,
    cbClsExtra: i32,
    cbWndExtra: i32,
    hInstance: ?HANDLE,
    hIcon: ?HANDLE,
    hCursor: ?HANDLE,
    hbrBackground: ?HANDLE,
    lpszMenuName: ?[*:0]const u16,
    lpszClassName: [*:0]const u16,
};

const PIPE_ACCESS_DUPLEX: u32 = 0x00000003;
const PIPE_TYPE_BYTE: u32 = 0x00000000;
const PIPE_READMODE_BYTE: u32 = 0x00000000;
const PIPE_WAIT: u32 = 0x00000000;
const NMPWAIT_USE_DEFAULT_WAIT: u32 = 0;
const ERROR_PIPE_CONNECTED: u32 = 535;
const GENERIC_READ: u32 = 0x80000000;
const INFINITE: u32 = 0xFFFFFFFF;
const WAIT_OBJECT_0: u32 = 0;
const WM_USER: u32 = 0x0400;
const WM_TBC_EXEC: u32 = WM_USER + 0x46;
const INVALID_HANDLE_VALUE: isize = -1;

// ---------------------------------------------------------------------------
// TJS2 structures (x86, pack 4)
// ---------------------------------------------------------------------------
// tTJSVariantString
const VSString = extern struct {
    RefCount: i32,
    LongString: ?[*]u16,
    ShortString: [22]u16,
    Length: i32,
    HeapFlag: u32,
    Hint: u32,
};

// tTJSVariant { union @0 (8B), vt @8 }
const Variant = extern struct {
    u: extern union {
        closure: extern struct {
            Object: ?*anyopaque,
            ObjThis: ?*anyopaque,
        },
        integer: i64,
        real: f64,
        string: ?*VSString,
        octet: ?*anyopaque,
    },
    vt: u32,
};
const tvtVoid: u32 = 0;
const tvtObject: u32 = 1;
const tvtString: u32 = 2;
const tvtOctet: u32 = 3;
const tvtInteger: u32 = 4;
const tvtReal: u32 = 5;

// tTJSString = single pointer to VSString
const TJSString = extern struct {
    Ptr: ?*VSString,
};

// tTVPExceptionDesc { ttstr type; ttstr message; }
const TVPExceptionDesc = extern struct {
    type_: TJSString,
    message: TJSString,
};

// iTVPFunctionExporter (2 virtuals, cdecl)
const iTVPFunctionExporter = extern struct {
    vtable: *const [2]usize,
};
const QueryNarrowFn = *const fn (
    self: *iTVPFunctionExporter,
    names: [*]const [*:0]const u8,
    funcs: [*]?*anyopaque,
    count: u32,
) callconv(.c) bool;
const QueryWideFn = *const fn (
    self: *iTVPFunctionExporter,
    names: [*]const [*:0]const u16,
    funcs: [*]?*anyopaque,
    count: u32,
) callconv(.c) bool;

// resolved engine functions
const GetScriptDispatchFn = *const fn () callconv(.c) ?*anyopaque;
const ExecuteScriptFn = *const fn (content: *const TJSString, result: ?*Variant) callconv(.c) void;
const AllocVariantStringFn = *const fn (str: [*:0]const u16) callconv(.c) ?*VSString;
const DoTryBlockFn = *const fn (
    tryblock: *const anyopaque,
    catchblock: *const anyopaque,
    finallyblock: ?*const anyopaque,
    data: ?*anyopaque,
) callconv(.c) void;

var exporter: ?*iTVPFunctionExporter = null;
var qNarrow: ?QueryNarrowFn = null;
var fnGetScriptDispatch: ?GetScriptDispatchFn = null;
var fnExecuteScript: ?ExecuteScriptFn = null;
var fnAllocVariantString: ?AllocVariantStringFn = null;
var fnDoTryBlock: ?DoTryBlockFn = null;

// ---------------------------------------------------------------------------
// logging
// ---------------------------------------------------------------------------
var logDir: ?[]u8 = null; // game directory (utf8), from module path
// all log calls happen on the main thread; no mutex needed

fn logStr(s: []const u8) void {
    const dir = logDir orelse return;
    const full = std.fs.path.join(alloc, &.{ dir, "tbc_bridge.log" }) catch return;
    defer alloc.free(full);
    appendUtf8ToFile(full, s);
}

extern "kernel32" fn CreateFileW(
    lpFileName: [*:0]const u16,
    dwDesiredAccess: u32,
    dwShareMode: u32,
    lpSecurityAttributes: ?*anyopaque,
    dwCreationDisposition: u32,
    dwFlagsAndAttributes: u32,
    hTemplateFile: ?HANDLE,
) callconv(STD_CALL) ?HANDLE;

const FILE_APPEND_DATA: u32 = 0x00000004;
const GENERIC_WRITE: u32 = 0x40000000;
const FILE_SHARE_READ: u32 = 0x1;
const FILE_SHARE_WRITE: u32 = 0x2;
const OPEN_ALWAYS: u32 = 4;
const FILE_ATTRIBUTE_NORMAL: u32 = 0x80;
const FILE_BEGIN: u32 = 0;
const FILE_END: u32 = 2;

extern "kernel32" fn SetFilePointer(
    hFile: HANDLE,
    lDistanceToMove: i32,
    lpDistanceToMoveHigh: ?*i32,
    dwMoveMethod: u32,
) callconv(STD_CALL) u32;
extern "kernel32" fn GetFileSize(hFile: HANDLE, lpFileSizeHigh: ?*u32) callconv(STD_CALL) u32;

fn appendUtf8ToFile(pathUtf8: []const u8, s: []const u8) void {
    const path16 = utf8ToUtf16(alloc, pathUtf8) catch return;
    defer alloc.free(path16);
    const h = CreateFileW(
        path16.ptr,
        GENERIC_WRITE,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        null,
        OPEN_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        null,
    );
    if (h == null) return;
    defer _ = CloseHandle(h.?);
    _ = SetFilePointer(h.?, 0, null, FILE_END);
    var written: u32 = 0;
    _ = WriteFile(h.?, s.ptr, @intCast(s.len), &written, null);
}

// ---------------------------------------------------------------------------
// request / response struct
// ---------------------------------------------------------------------------
const EvalRequest = struct {
    code: []u8, // user code (utf8)
    done: HANDLE, // completion event
    ok: bool,
    result: []u8, // utf8 result (allocated on main thread)
    errmsg: []u8, // utf8 error message
    logline: []const u8,
};

// ---------------------------------------------------------------------------
// UTF conversions (BMP only, fine for JP/CN)
// ---------------------------------------------------------------------------
fn utf8ToUtf16(allocator: std.mem.Allocator, s: []const u8) ![:0]u16 {
    const out = try allocator.alloc(u16, s.len + 1);
    var j: usize = 0;
    var i: usize = 0;
    while (i < s.len) {
        const c = try std.unicode.utf8Decode(s[i..]);
        out[j] = @truncate(c);
        j += 1;
        i += std.unicode.utf8ByteSequenceLength(s[i]) catch 1;
    }
    out[j] = 0;
    return out[0..j :0];
}

fn utf16ToUtf8(allocator: std.mem.Allocator, s: []const u16) ![]u8 {
    // worst case: 3 bytes per BMP code unit
    const out = try allocator.alloc(u8, s.len * 3);
    var j: usize = 0;
    for (s) |c| {
        var tmp: [4]u8 = undefined;
        const n: usize = std.unicode.utf8Encode(c, &tmp) catch blk: {
            tmp = [_]u8{ 0xEF, 0xBF, 0xBD, 0 }; // U+FFFD
            break :blk 3;
        };
        @memcpy(out[j .. j + n], tmp[0..n]);
        j += n;
    }
    return allocator.realloc(out, j);
}

fn vsStringToUtf8(allocator: std.mem.Allocator, vs: *VSString) ![]u8 {
    const n: usize = @intCast(@max(vs.Length, 0));
    const data: [*]const u16 = if (vs.LongString) |l| l else &vs.ShortString;
    return utf16ToUtf8(allocator, data[0..n]);
}

fn formatI64(allocator: std.mem.Allocator, v: i64) ![]u8 {
    return std.fmt.allocPrint(allocator, "{d}", .{v});
}

fn formatF64(allocator: std.mem.Allocator, v: f64) ![]u8 {
    return std.fmt.allocPrint(allocator, "{d}", .{v});
}

// ---------------------------------------------------------------------------
// TVPDoTryBlock callbacks (cdecl)
// ---------------------------------------------------------------------------
const TryData = struct {
    ttstr: TJSString,
    result: Variant,
    errmsg: []u8 = &.{},
};

fn tryBlock(dataPtr: ?*anyopaque) callconv(.c) void {
    const d: *TryData = @ptrCast(@alignCast(dataPtr.?));
    if (fnExecuteScript) |f| f(&d.ttstr, &d.result);
}

fn catchBlock(dataPtr: ?*anyopaque, desc: *const TVPExceptionDesc) callconv(.c) bool {
    const d: *TryData = @ptrCast(@alignCast(dataPtr.?));
    if (desc.message.Ptr) |vs| {
        d.errmsg = vsStringToUtf8(alloc, vs) catch &.{};
    } else {
        d.errmsg = alloc.dupe(u8, "unknown TJS exception") catch &.{};
    }
    return false; // swallow
}

// ---------------------------------------------------------------------------
// eval core (runs on main thread via WndProc)
// ---------------------------------------------------------------------------
fn doEval(codeUtf8: []const u8) EvalResult {
    if (fnGetScriptDispatch == null or fnExecuteScript == null or fnAllocVariantString == null or fnDoTryBlock == null) {
        return .{ .ok = false, .errmsg = alloc.dupe(u8, "engine functions not resolved") catch &.{} };
    }
    // lazy check: engine must be initialized
    const g = fnGetScriptDispatch.?();
    if (g == null) {
        return .{ .ok = false, .errmsg = alloc.dupe(u8, "TJS engine not ready (game still starting)") catch &.{} };
    }
    // build wrapper script
    const wrapper = buildWrapper(codeUtf8) catch return .{ .ok = false, .errmsg = alloc.dupe(u8, "wrapper build failed") catch &.{} };
    defer alloc.free(wrapper);
    const w16 = utf8ToUtf16(alloc, wrapper) catch return .{ .ok = false, .errmsg = alloc.dupe(u8, "utf8 conv failed") catch &.{} };
    defer alloc.free(w16);
    const vs = fnAllocVariantString.?(@ptrCast(w16)) orelse {
        return .{ .ok = false, .errmsg = alloc.dupe(u8, "TJSAllocVariantString failed") catch &.{} };
    };
    var data = TryData{ .ttstr = .{ .Ptr = vs }, .result = std.mem.zeroes(Variant) };
    fnDoTryBlock.?(
        @ptrCast(&tryBlock),
        @ptrCast(&catchBlock),
        null,
        @ptrCast(&data),
    );
    if (data.errmsg.len > 0) {
        return .{ .ok = false, .errmsg = data.errmsg };
    }
    // decode result variant
    const r = &data.result;
    if (r.vt == tvtString) {
        if (r.u.string) |vs2| {
            return .{ .ok = true, .result = vsStringToUtf8(alloc, vs2) catch &.{} };
        }
        return .{ .ok = true, .result = alloc.dupe(u8, "null string") catch &.{} };
    } else if (r.vt == tvtInteger) {
        return .{ .ok = true, .result = formatI64(alloc, r.u.integer) catch &.{} };
    } else if (r.vt == tvtReal) {
        return .{ .ok = true, .result = formatF64(alloc, r.u.real) catch &.{} };
    } else if (r.vt == tvtVoid) {
        return .{ .ok = true, .result = alloc.dupe(u8, "undefined") catch &.{} };
    } else {
        return .{ .ok = true, .result = alloc.dupe(u8, "[non-string result]") catch &.{} };
    }
}

const EvalResult = struct {
    ok: bool,
    result: []u8 = &.{},
    errmsg: []u8 = &.{},
};

fn buildWrapper(codeUtf8: []const u8) ![]u8 {
    // single-line wrapper; user code inserted verbatim inside a closure
    const pre = "var __tbc_r;try{__tbc_r=(function(){";
    const post = "})();}catch(__tbc_e){__tbc_r=\"!!EXCEPTION!! \"+__tbc_e;}__tbc_r===void?\"undefined\":String(__tbc_r)";
    const total = pre.len + codeUtf8.len + post.len;
    const out = try alloc.alloc(u8, total);
    @memcpy(out[0..pre.len], pre);
    @memcpy(out[pre.len .. pre.len + codeUtf8.len], codeUtf8);
    @memcpy(out[pre.len + codeUtf8.len ..], post);
    return out;
}

// ---------------------------------------------------------------------------
// WndProc (main thread)
// ---------------------------------------------------------------------------
fn wndProc(hWnd: HWND, msg: u32, wParam: WPARAM, lParam: LPARAM) callconv(STD_CALL) LPARAM {
    if (msg == WM_TBC_EXEC) {
        const req: *EvalRequest = @ptrFromInt(@as(usize, @bitCast(lParam)));
        const res = doEval(req.code);
        req.ok = res.ok;
        req.result = res.result;
        req.errmsg = res.errmsg;
        logStr(req.logline);
        _ = SetEvent(req.done);
        return 0;
    }
    if (msg == 0x0111) { // WM_COMMAND ignore
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

// ---------------------------------------------------------------------------
// pipe server thread
// ---------------------------------------------------------------------------
const PIPE_NAME = "\\\\.\\pipe\\tbc_bridge";
const MAX_CMD: usize = 512 * 1024;

var bridgeHwnd: ?HWND = null;

fn pipeThread(_arg: ?*anyopaque) callconv(.c) u32 {
    _ = _arg;
    var pipeNameBuf: [32]u16 = undefined;
    var i: usize = 0;
    for (PIPE_NAME) |ch| {
        pipeNameBuf[i] = ch;
        i += 1;
    }
    pipeNameBuf[i] = 0;
    const name: [*:0]const u16 = @ptrCast(&pipeNameBuf);

    while (true) {
        const hPipe = CreateNamedPipeW(
            name,
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
            1,
            1024 * 1024,
            1024 * 1024,
            NMPWAIT_USE_DEFAULT_WAIT,
            null,
        );
        if (hPipe == null) {
            sleepMs(100);
            continue;
        }
        const ok = ConnectNamedPipe(hPipe.?, null);
        if (ok == 0) {
            const err = GetLastError();
            if (err != ERROR_PIPE_CONNECTED) {
                _ = CloseHandle(hPipe.?);
                continue;
            }
        }
        handleClient(hPipe.?);
        _ = DisconnectNamedPipe(hPipe.?);
        _ = CloseHandle(hPipe.?);
    }
    return 0;
}

fn handleClient(hPipe: HANDLE) void {
    var buf: [16]u8 = undefined;
    var got: u32 = 0;
    // read command line (until \n)
    var cmd: [16]u8 = undefined;
    var cmdLen: usize = 0;
    while (cmdLen < 16) {
        got = 0;
        if (ReadFile(hPipe, buf[0..1].ptr, 1, &got, null) == 0 or got == 0) return;
        if (buf[0] == '\n') break;
        cmd[cmdLen] = buf[0];
        cmdLen += 1;
    }
    const cmdStr = cmd[0..cmdLen];
    if (std.mem.eql(u8, cmdStr, "PING")) {
        _ = writeAll(hPipe, "PONG\n") catch 0;
        return;
    }
    if (!std.mem.eql(u8, cmdStr, "EVAL")) {
        _ = writeAll(hPipe, "ERR\nbad command") catch 0;
        return;
    }
    // read 4-byte LE length
    got = 0;
    var lenBytes: [4]u8 = undefined;
    var total: u32 = 0;
    while (total < 4) {
        got = 0;
        if (ReadFile(hPipe, lenBytes[total..4].ptr, 4 - total, &got, null) == 0 or got == 0) return;
        total += got;
    }
    const codeLen: usize = std.mem.readInt(u32, &lenBytes, .little);
    if (codeLen > MAX_CMD) {
        _ = writeAll(hPipe, "ERR\ncode too long") catch 0;
        return;
    }
    const code = alloc.alloc(u8, codeLen) catch {
        _ = writeAll(hPipe, "ERR\nalloc failed") catch 0;
        return;
    };
    defer alloc.free(code);
    total = 0;
    while (total < codeLen) {
        got = 0;
        if (ReadFile(hPipe, code[total..codeLen].ptr, @intCast(codeLen - total), &got, null) == 0 or got == 0) return;
        total += got;
    }
    // build request and post to main thread
    const ev = CreateEventW(null, 0, 0, null) orelse {
        _ = writeAll(hPipe, "ERR\nno event") catch 0;
        return;
    };
    defer _ = CloseHandle(ev);
    const logline = std.fmt.allocPrint(alloc, "EVAL [{d} bytes]: {s}\n", .{ codeLen, code }) catch alloc.dupe(u8, "EVAL\n") catch &.{};
    const req = alloc.create(EvalRequest) catch {
        _ = writeAll(hPipe, "ERR\nno mem") catch 0;
        return;
    };
    req.* = .{
        .code = code,
        .done = ev,
        .ok = false,
        .result = &.{},
        .errmsg = &.{},
        .logline = logline,
    };
    const hw = bridgeHwnd orelse {
        alloc.destroy(req);
        _ = writeAll(hPipe, "ERR\nno window") catch 0;
        return;
    };
    _ = PostMessageW(hw, WM_TBC_EXEC, 0, @bitCast(@as(isize, @intCast(@intFromPtr(req)))));
    const waitRes = WaitForSingleObject(ev, 30 * 1000);
    if (waitRes == WAIT_OBJECT_0) {
        if (req.ok) {
            const head = "OK\n" ++ [_]u8{0} ++ [_]u8{0} ++ [_]u8{0} ++ [_]u8{0};
            _ = writeAll(hPipe, head[0..4]) catch {};
            _ = writeAllLen(hPipe, req.result) catch {};
        } else {
            _ = writeAll(hPipe, "ERR\n") catch {};
            _ = writeAllLen(hPipe, req.errmsg) catch {};
        }
    } else {
        _ = writeAll(hPipe, "ERR\ntimeout") catch {};
    }
    if (req.result.len > 0) alloc.free(req.result);
    if (req.errmsg.len > 0) alloc.free(req.errmsg);
    if (req.logline.len > 0) alloc.free(req.logline);
    alloc.destroy(req);
}

fn writeAll(hPipe: HANDLE, s: []const u8) !void {
    var off: usize = 0;
    while (off < s.len) {
        var written: u32 = 0;
        const ok = WriteFile(hPipe, s.ptr + off, @intCast(s.len - off), &written, null);
        if (ok == 0 or written == 0) return error.WriteFailed;
        off += written;
    }
}

fn writeAllLen(hPipe: HANDLE, s: []const u8) !void {
    var lenBuf: [4]u8 = undefined;
    std.mem.writeInt(u32, &lenBuf, @intCast(s.len), .little);
    try writeAll(hPipe, &lenBuf);
    try writeAll(hPipe, s);
}

// ---------------------------------------------------------------------------
// V2Link / V2Unlink (exported)
// ---------------------------------------------------------------------------
fn V2LinkImpl(exp: ?*iTVPFunctionExporter) callconv(STD_CALL) i32 {
    if (exp == null) return 0;
    exporter = exp;
    // resolve exporter vtable methods
    const v: *const [2]usize = exp.?.vtable;
    qNarrow = @ptrFromInt(v[1]);
    // resolve engine functions by signature name
    const names = [_][*:0]const u8{
        "iTJSDispatch2 * ::TVPGetScriptDispatch()",
        "void ::TVPExecuteScript(const ttstr &,tTJSVariant *)",
        "tTJSVariantString * ::TJSAllocVariantString(const tjs_char *)",
        "void ::TVPDoTryBlock(tTVPTryBlockFunction,tTVPCatchBlockFunction,tTVPFinallyBlockFunction,void *)",
    };
    var funcs: [names.len]?*anyopaque = .{ null, null, null, null };
    if (qNarrow) |qn| { _ = qn(exp.?, &names, &funcs, names.len); }
    fnGetScriptDispatch = @ptrCast(funcs[0]);
    fnExecuteScript = @ptrCast(funcs[1]);
    fnAllocVariantString = @ptrCast(funcs[2]);
    fnDoTryBlock = @ptrCast(funcs[3]);
    // write log header
    var pathBuf: [1024]u16 = undefined;
    const n = GetModuleFileNameW(null, &pathBuf, 1024);
    if (n > 0) {
        // strip file name -> game dir (utf8)
        var path16 = pathBuf[0..n];
        while (path16.len > 0 and path16[path16.len - 1] != '\\') {
            path16 = path16[0 .. path16.len - 1];
        }
        const dirUtf8 = utf16ToUtf8(alloc, path16) catch null;
        if (dirUtf8) |d| {
            logDir = d;
            const pid = GetCurrentProcessId();
            logStr(std.fmt.allocPrint(alloc, "=== tbc_bridge V2Link pid={d} ===\n", .{pid}) catch "=== tbc_bridge V2Link ===\n");
            logStr(std.fmt.allocPrint(alloc, "exporter={x}\n", .{@intFromPtr(exp.?)}) catch "");
            logStr(std.fmt.allocPrint(alloc, "TVPGetScriptDispatch={x} TVPExecuteScript={x} TJSAllocVariantString={x} TVPDoTryBlock={x}\n", .{ if (funcs[0]) |x| @intFromPtr(x) else 0, if (funcs[1]) |x| @intFromPtr(x) else 0, if (funcs[2]) |x| @intFromPtr(x) else 0, if (funcs[3]) |x| @intFromPtr(x) else 0 }) catch "");
        }
    }
    // create hidden window (main thread)
    var clsBuf: [16]u16 = undefined;
    var idx: usize = 0;
    for ("tbc_bridge") |ch| {
        clsBuf[idx] = ch;
        idx += 1;
    }
    clsBuf[idx] = 0;
    const clsName: [*:0]const u16 = @ptrCast(&clsBuf);
    const wc = WNDCLASSW{
        .style = 0,
        .lpfnWndProc = &wndProc,
        .cbClsExtra = 0,
        .cbWndExtra = 0,
        .hInstance = null,
        .hIcon = null,
        .hCursor = null,
        .hbrBackground = null,
        .lpszMenuName = null,
        .lpszClassName = clsName,
    };
    const classId = RegisterClassW(&wc);
    if (classId != 0) {
        bridgeHwnd = CreateWindowExW(0, clsName, clsName, 0, 0, 0, 0, 0, null, null, null, null);
        if (bridgeHwnd == null) {
            logStr("CreateWindowExW failed\n");
        }
    } else {
        logStr("RegisterClassW failed\n");
    }
    // start pipe server thread
    const th = CreateThread(null, 0, &pipeThread, null, 0, null);
    _ = th;
    return 0;
}

fn V2UnlinkImpl() callconv(.c) i32 {
    if (bridgeHwnd) |h| _ = DestroyWindow(h);
    logStr("=== tbc_bridge V2Unlink ===\n");
    return 0;
}















// The engine looks up GetProcAddress("V2Link") with an UNDECORATED name and
// calls it as __stdcall. Zig's automatic export on i386 decorates stdcall
// symbols (V2Link@4), so we export a naked shim under the plain name that
// performs the stdcall thunk dance: push arg, call cdecl wrapper, clean the
// arg, return the impl's value, then `ret 4` (stdcall: pop retaddr + arg).
fn V2LinkImplCdecl(expPtr: ?*iTVPFunctionExporter) callconv(.c) i32 {
    return V2LinkImpl(expPtr);
}

var g_impl_cdecl: *const fn (?*iTVPFunctionExporter) callconv(.c) i32 = &V2LinkImplCdecl;

fn V2LinkShim() callconv(.naked) i32 {
    asm volatile (
        \\.intel_syntax noprefix
        \\ push ecx
        \\ mov ecx, [esp+8]
        \\ push ecx
        \\ call dword ptr [%[ptr]]
        \\ add esp, 4
        \\ mov [esp], eax
        \\ pop eax
        \\ ret 4
        :
        : [ptr] "m" (g_impl_cdecl),
    );
}

comptime {
    @export(&V2LinkShim, .{ .name = "V2Link" });
    @export(&V2UnlinkImpl, .{ .name = "V2Unlink" });
}







