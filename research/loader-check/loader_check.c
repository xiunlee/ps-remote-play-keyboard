/* Isolated loader diagnostics. This executable can only start test threads in
 * itself and in the exact child process it creates. It never opens another PID,
 * copies executable code, reads process memory, or calls StartProbe. */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <wchar.h>

#define PATH_CAP 32768
#define WAIT_MS 10000
typedef struct Shared {
    WCHAR path[PATH_CAP];
    uint64_t self, path_address, wrapper, inspect, load_library;
    uint64_t module;
    DWORD error, export_found, unloaded;
    volatile LONG ready, finish;
} Shared;
static unsigned failures, checks;

static void check(int ok, const char *label) {
    checks++; if (!ok) failures++;
    printf("%s %s\n", ok ? "PASS" : "FAIL", label); fflush(stdout);
}
static void clear_result(Shared *s) {
    s->module=0; s->error=0; s->export_found=0; s->unloaded=0;
}
static void inspect_and_unload(Shared *s, HMODULE module) {
    s->module=(uint64_t)(uintptr_t)module;
    if (module) {
        s->export_found=GetProcAddress(module,"StartProbe")!=NULL;
        s->unloaded=FreeLibrary(module) && GetModuleHandleW(s->path)==NULL;
    }
}
static DWORD WINAPI load_and_report(LPVOID argument) {
    Shared *s=(Shared*)argument;
    HMODULE module;
    SetLastError(0);
    module=LoadLibraryW(s->path);
    /* GetLastError is meaningful on failure and must be captured in this thread. */
    s->error=module ? 0 : GetLastError();
    inspect_and_unload(s,module);
    return s->error;
}
static DWORD WINAPI inspect_existing(LPVOID argument) {
    Shared *s=(Shared*)argument;
    HMODULE module=GetModuleHandleW(s->path);
    inspect_and_unload(s,module);
    return module ? 0 : ERROR_MOD_NOT_FOUND;
}
static int join_thread(HANDLE thread, DWORD *result) {
    int ok;
    if (!thread) {printf("Create thread error=%lu\n",GetLastError());return 0;}
    ok=WaitForSingleObject(thread,WAIT_MS)==WAIT_OBJECT_0;
    if (ok) ok=GetExitCodeThread(thread,result)!=0;
    CloseHandle(thread);return ok;
}
static void inspect_result(Shared *s, int missing, const char *label) {
    printf("RESULT %s module=0x%llX error=%lu export=%lu unloaded=%lu\n",label,
        (unsigned long long)s->module,s->error,s->export_found,s->unloaded);
    check(missing ? s->module==0 && s->error==ERROR_MOD_NOT_FOUND :
        s->module!=0 && s->error==0 && s->export_found==1 && s->unloaded==1,label);
}
static int child_main(const WCHAR *mapname) {
    HANDLE map=OpenFileMappingW(FILE_MAP_ALL_ACCESS,FALSE,mapname);
    Shared *s;ULONGLONG deadline;
    if(!map)return 2;
    s=(Shared*)MapViewOfFile(map,FILE_MAP_ALL_ACCESS,0,0,sizeof(Shared));
    if(!s){CloseHandle(map);return 3;}
    s->self=(uint64_t)(uintptr_t)s;
    s->path_address=(uint64_t)(uintptr_t)s->path;
    s->wrapper=(uint64_t)(uintptr_t)load_and_report;
    s->inspect=(uint64_t)(uintptr_t)inspect_existing;
    s->load_library=(uint64_t)(uintptr_t)GetProcAddress(GetModuleHandleW(L"kernel32.dll"),"LoadLibraryW");
    MemoryBarrier();InterlockedExchange(&s->ready,1);
    deadline=GetTickCount64()+45000;
    while(!InterlockedCompareExchange(&s->finish,0,0) && GetTickCount64()<deadline)Sleep(10);
    UnmapViewOfFile(s);CloseHandle(map);return 0;
}
static int child_test(const WCHAR *dllpath, int missing, int raw) {
    WCHAR executable[PATH_CAP],mapname[160],command[PATH_CAP+256];
    STARTUPINFOW si;PROCESS_INFORMATION pi;HANDLE map=NULL,thread;Shared *s=NULL;
    DWORD result=0,path_length;int ok=0,created=0;ULONGLONG deadline;const char *label;
    label=raw ? (missing?"remote raw missing":"remote raw probe") :
        (missing?"remote wrapper missing":"remote wrapper probe");
    path_length=GetModuleFileNameW(NULL,executable,PATH_CAP);
    if(!path_length || path_length>=PATH_CAP)goto cleanup;
    swprintf(mapname,160,L"Local\\PSKBLoaderCheck-%lu-%llu-%d-%d",GetCurrentProcessId(),
        (unsigned long long)GetTickCount64(),missing,raw);
    map=CreateFileMappingW(INVALID_HANDLE_VALUE,NULL,PAGE_READWRITE,0,sizeof(Shared),mapname);
    if(!map || GetLastError()==ERROR_ALREADY_EXISTS)goto cleanup;
    s=(Shared*)MapViewOfFile(map,FILE_MAP_ALL_ACCESS,0,0,sizeof(Shared));if(!s)goto cleanup;
    ZeroMemory(s,sizeof(*s));wcscpy(s->path,dllpath);
    ZeroMemory(&si,sizeof(si));si.cb=sizeof(si);ZeroMemory(&pi,sizeof(pi));
    swprintf(command,PATH_CAP+256,L"\"%ls\" --child \"%ls\"",executable,mapname);
    /* The target handle comes directly from launching this same executable.
     * There is deliberately no PID argument and no OpenProcess API. */
    if(!CreateProcessW(executable,command,NULL,NULL,FALSE,CREATE_NO_WINDOW,NULL,NULL,&si,&pi))goto cleanup;
    created=1;CloseHandle(pi.hThread);deadline=GetTickCount64()+WAIT_MS;
    while(!InterlockedCompareExchange(&s->ready,0,0) && GetTickCount64()<deadline &&
        WaitForSingleObject(pi.hProcess,0)==WAIT_TIMEOUT)Sleep(10);
    if(!s->ready)goto cleanup;
    thread=CreateRemoteThread(pi.hProcess,NULL,0,
        (LPTHREAD_START_ROUTINE)(uintptr_t)(raw?s->load_library:s->wrapper),
        (LPVOID)(uintptr_t)(raw?s->path_address:s->self),0,NULL);
    if(!join_thread(thread,&result))goto cleanup;
    if(raw) {
        DWORD inspect_result_code=0;
        thread=CreateRemoteThread(pi.hProcess,NULL,0,(LPTHREAD_START_ROUTINE)(uintptr_t)s->inspect,
            (LPVOID)(uintptr_t)s->self,0,NULL);
        if(!join_thread(thread,&inspect_result_code))goto cleanup;
        /* A raw LoadLibraryW thread returns only a DWORD; inspect in the child
         * to get the full module pointer. Its remote GetLastError is unknown. */
        printf("RESULT %s threadDWORD=0x%08lX fullModule=0x%llX export=%lu unloaded=%lu\n",
            label,result,(unsigned long long)s->module,s->export_found,s->unloaded);
        ok=missing ? result==0 && s->module==0 && inspect_result_code==ERROR_MOD_NOT_FOUND :
            s->module!=0 && result==(DWORD)s->module && s->export_found==1 && s->unloaded==1;
        check(ok,label);
    } else {
        inspect_result(s,missing,label);
        ok=missing ? result==ERROR_MOD_NOT_FOUND && s->module==0 :
            result==0 && s->module!=0 && s->export_found==1 && s->unloaded==1;
    }
cleanup:
    if(created) {
        if(s)InterlockedExchange(&s->finish,1);
        if(WaitForSingleObject(pi.hProcess,5000)!=WAIT_OBJECT_0) {
            /* Only terminate this diagnostic's own child after a bounded wait. */
            TerminateProcess(pi.hProcess,99);WaitForSingleObject(pi.hProcess,5000);ok=0;
        }
        CloseHandle(pi.hProcess);
    }
    if(s)UnmapViewOfFile(s);if(map)CloseHandle(map);
    if(!ok)check(0,"child diagnostic completed without setup/timeout failure");
    return ok;
}
int wmain(int argc,WCHAR **argv) {
    Shared *s;WCHAR dllpath[PATH_CAP],missing[PATH_CAP];DWORD result,path_length;HANDLE thread;int i;
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX);
    if(argc==3 && wcscmp(argv[1],L"--child")==0)return child_main(argv[2]);
    if(argc!=2){puts("Usage: pskb-loader-check.exe <absolute probe DLL path>");return 2;}
    path_length=GetFullPathNameW(argv[1],PATH_CAP,dllpath,NULL);
    if(!path_length || path_length>PATH_CAP-80 ||
       GetFileAttributesW(dllpath)==INVALID_FILE_ATTRIBUTES){puts("Invalid probe DLL path");return 2;}
    swprintf(missing,PATH_CAP,L"%ls.missing-%lu",dllpath,GetCurrentProcessId());
    if(GetFileAttributesW(missing)!=INVALID_FILE_ATTRIBUTES){puts("Missing-file control unexpectedly exists");return 2;}
    s=(Shared*)HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,sizeof(*s));if(!s)return 3;
    for(i=0;i<2;i++) {
        wcscpy(s->path,i?dllpath:missing);clear_result(s);
        load_and_report(s);inspect_result(s,!i,i?"direct probe":"direct missing");
        clear_result(s);thread=CreateThread(NULL,0,load_and_report,s,0,NULL);
        if(!join_thread(thread,&result)){puts("Local thread timeout/failure");ExitProcess(99);}
        inspect_result(s,!i,i?"local thread probe":"local thread missing");
        child_test(s->path,!i,0);child_test(s->path,!i,1);
    }
    HeapFree(GetProcessHeap(),0,s);
    printf("SUMMARY checks=%u failures=%u StartProbe_calls=0 external_processes_opened=0\n",checks,failures);
    return failures?1:0;
}
