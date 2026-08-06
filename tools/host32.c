// host32.c â€?fake engine host to observe what functions mtool's tjsjson32.dll queries
#include <windows.h>
#include <stdio.h>

typedef int (__cdecl *QueryFn)(void *self, const char **names, void **funcs, unsigned int count);
typedef int (__cdecl *QueryWideFn)(void *self, const wchar_t **names, void **funcs, unsigned int count);

struct Exporter {
    void *vtable[2]; // actually just the vptr; extra slack
};

static int __cdecl queryNarrow(void *self, const char **names, void **funcs, unsigned int count)
{
    printf("== QueryFunctionsByNarrowString(count=%u) ==\n", count);
    for (unsigned i = 0; i < count; i++) {
        printf("  [%u] '%s'\n", i, names[i]);
        funcs[i] = (void *)0x12340000; // fake function pointers
    }
    return 1;
}

static int __cdecl queryWide(void *self, const wchar_t **names, void **funcs, unsigned int count)
{
    printf("== QueryFunctions(count=%u) ==\n", count);
    for (unsigned i = 0; i < count; i++) {
        wprintf(L"  [%u] '%ls'\n", i, names[i]);
        funcs[i] = (void *)0x12340000;
    }
    return 1;
}

int main(void)
{
    HMODULE h = LoadLibraryA("tjsjson32.dll");
    if (!h) { printf("load failed err=%lu\n", GetLastError()); return 1; }
    void *v2 = (void *)GetProcAddress(h, "V2Link");
    if (!v2) { printf("no V2Link export\n"); return 1; }
    printf("V2Link at %p\n", v2);

    typedef int (__stdcall *V2LinkFn)(void *exporter);
    V2LinkFn v2link = (V2LinkFn)v2;

    // C++ object with a single vptr; vptr -> vtable; vtable[0]=QueryFunctions (wide),
    // vtable[1]=QueryFunctionsByNarrowString
    void *vtable[2] = { (void *)queryWide, (void *)queryNarrow };
    struct Exporter exp;
    exp.vtable[0] = (void *)vtable;
    exp.vtable[1] = NULL;

    printf("calling V2Link...\n");
    fflush(stdout);
    int rc = v2link(&exp);
    printf("V2Link returned %d\n", rc);
    fflush(stdout);
    Sleep(5000);
    printf("done\n");
    return 0;
}

