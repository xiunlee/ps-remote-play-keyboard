/* Narrow, observation-only x64 HID API probe. No report bytes are inspected
 * or changed. Only independently allowlisted and locally revalidated DS4
 * handles are counted. Test mode is limited to pskb-probe-test.exe. */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winternl.h>
#include <hidsdi.h>
#include <hidpi.h>
#include <stdint.h>
#include <wchar.h>
#include "MinHook.h"
#include "shared.h"

#define MAX_PENDING 128
#define MAX_PARSED 32
#define STATUS_PENDING_VALUE ((NTSTATUS)0x00000103L)
#define STATUS_SUCCESS_VALUE ((NTSTATUS)0)
typedef VOID (NTAPI *ProbeApc)(PVOID, PIO_STATUS_BLOCK, ULONG);
typedef NTSTATUS (NTAPI *NtReadFn)(HANDLE,HANDLE,ProbeApc,PVOID,PIO_STATUS_BLOCK,PVOID,ULONG,PLARGE_INTEGER,PULONG);
typedef NTSTATUS (NTAPI *NtCloseFn)(HANDLE);
typedef NTSTATUS (NTAPI *NtWaitFn)(HANDLE,BOOLEAN,PLARGE_INTEGER);
typedef BOOL (WINAPI *ReadFn)(HANDLE,LPVOID,DWORD,LPDWORD,LPOVERLAPPED);
typedef BOOL (WINAPI *ReadExFn)(HANDLE,LPVOID,DWORD,LPOVERLAPPED,LPOVERLAPPED_COMPLETION_ROUTINE);
typedef BOOL (WINAPI *ResultFn)(HANDLE,LPOVERLAPPED,LPDWORD,BOOL);
typedef BOOL (WINAPI *ResultExFn)(HANDLE,LPOVERLAPPED,LPDWORD,DWORD,BOOL);
typedef BOOL (WINAPI *IocpFn)(HANDLE,LPDWORD,PULONG_PTR,LPOVERLAPPED*,DWORD);
typedef BOOLEAN (WINAPI *InputFn)(HANDLE,PVOID,ULONG);
typedef BOOLEAN (WINAPI *PreparsedFn)(HANDLE,PHIDP_PREPARSED_DATA*);
typedef BOOLEAN (WINAPI *FreePreparsedFn)(PHIDP_PREPARSED_DATA);
typedef NTSTATUS (WINAPI *UsageFn)(HIDP_REPORT_TYPE,USAGE,USHORT,USAGE,PULONG,PHIDP_PREPARSED_DATA,PCHAR,ULONG);
typedef NTSTATUS (WINAPI *UsagesFn)(HIDP_REPORT_TYPE,USAGE,USHORT,PUSAGE,PULONG,PHIDP_PREPARSED_DATA,PCHAR,ULONG);

static NtReadFn real_nt_read;
static NtCloseFn real_nt_close;
static NtWaitFn real_nt_wait;
static ReadFn real_read;
static ReadExFn real_read_ex;
static ResultFn real_result;
static ResultExFn real_result_ex;
static IocpFn real_iocp;
static InputFn real_input;
static PreparsedFn real_preparsed;
static FreePreparsedFn real_free_preparsed;
static UsageFn real_usage;
static UsagesFn real_usages;
static PskbShared *shared;
static HANDLE mapping_handle;
static volatile LONG enabled;
static volatile LONG started;
static HANDLE allowed[PSKB_MAX_HANDLES];
static SRWLOCK data_lock = SRWLOCK_INIT;
typedef struct Pending { HANDLE handle, event; PVOID operation, context, buffer; ULONG length; } Pending;
typedef struct Parsed { HANDLE handle; PHIDP_PREPARSED_DATA data; } Parsed;
static Pending pending[MAX_PENDING];
static Parsed parsed[MAX_PARSED];

static int is_allowed(HANDLE h) {
    int found=0; unsigned i;
    if (!InterlockedCompareExchange(&enabled,0,0)) return 0;
    AcquireSRWLockShared(&data_lock);
    for(i=0;i<PSKB_MAX_HANDLES;i++) if(allowed[i] && allowed[i]==h) {found=1;break;}
    ReleaseSRWLockShared(&data_lock);
    return found;
}
static void count(int id) { InterlockedIncrement(&shared->counts[id]); }
static void caller(unsigned id, PVOID p) { InterlockedExchange64((volatile LONG64*)&shared->last_caller[id],(LONG64)(uintptr_t)p); }
static void remember(HANDLE h,HANDLE event,PVOID op,PVOID context,PVOID buffer,ULONG length) {
    unsigned i,slot=MAX_PENDING;
    if(!op) return;
    AcquireSRWLockExclusive(&data_lock);
    for(i=0;i<MAX_PENDING;i++) {
        if(pending[i].operation==op) {slot=i;break;}
        if(!pending[i].operation && slot==MAX_PENDING) slot=i;
    }
    if(slot<MAX_PENDING) {
        Pending *p=&pending[slot]; p->handle=h; p->event=(HANDLE)((uintptr_t)event&~(uintptr_t)1);
        p->operation=op;p->context=context;p->buffer=buffer;p->length=length;
    } else count(CNT_RESERVED); /* Observation table full; never change I/O. */
    ReleaseSRWLockExclusive(&data_lock);
}
static int forget(PVOID op) {
    unsigned i; int found=0;
    if(!op) return 0;
    AcquireSRWLockExclusive(&data_lock);
    for(i=0;i<MAX_PENDING;i++) if(pending[i].operation==op || pending[i].context==op) {
        ZeroMemory(&pending[i],sizeof(pending[i]));found=1;
    }
    ReleaseSRWLockExclusive(&data_lock);return found;
}
static int known_event(HANDLE event) {
    unsigned i;int found=0;
    AcquireSRWLockShared(&data_lock);
    for(i=0;i<MAX_PENDING;i++) if(pending[i].operation &&
        (pending[i].event==event || (!pending[i].event && pending[i].handle==event))) {found=1;break;}
    ReleaseSRWLockShared(&data_lock);return found;
}
static int known_parse(PHIDP_PREPARSED_DATA data,PCHAR report,ULONG length) {
    unsigned i;int found=0;
    if(length!=64 || !InterlockedCompareExchange(&enabled,0,0)) return 0;
    AcquireSRWLockShared(&data_lock);
    for(i=0;i<MAX_PARSED;i++) if(parsed[i].data==data && data) {found=1;break;}
    /* This is only an observation correlation. Never use a matching buffer
     * address alone to authorize future input modification. */
    if(!found) for(i=0;i<MAX_PENDING;i++) if(pending[i].operation && pending[i].buffer==report && pending[i].length==64) {found=1;break;}
    ReleaseSRWLockShared(&data_lock);return found;
}

static NTSTATUS NTAPI hook_nt_read(HANDLE h,HANDLE event,ProbeApc apc,PVOID context,PIO_STATUS_BLOCK iosb,PVOID buffer,ULONG length,PLARGE_INTEGER offset,PULONG key) {
    DWORD incoming=GetLastError(),outgoing;int tracked=is_allowed(h); NTSTATUS result;
    if(tracked) {count(CNT_NT_READ);caller(0,__builtin_return_address(0));shared->report_len=length;
        if(apc) caller(7,(PVOID)(uintptr_t)apc);
        remember(h,event,iosb,context,buffer,length);}
    SetLastError(incoming);
    result=real_nt_read(h,event,apc,context,iosb,buffer,length,offset,key);outgoing=GetLastError();
    if(tracked) {
        shared->last_nt_status=(uint32_t)result;
        if(result==STATUS_PENDING_VALUE) count(CNT_NT_PENDING);
        else if(result>=0) count(CNT_NT_SUCCESS);
        else {count(CNT_NT_FAILURE);forget(iosb);}
    }
    SetLastError(outgoing);return result;
}
static BOOL WINAPI hook_read(HANDLE h,LPVOID b,DWORD n,LPDWORD done,LPOVERLAPPED ov) {
    BOOL result;DWORD incoming=GetLastError(),error;int tracked=is_allowed(h);
    if(tracked) {count(CNT_READFILE);caller(1,__builtin_return_address(0));if(ov)remember(h,ov->hEvent,ov,ov,b,n);}
    SetLastError(incoming);result=real_read(h,b,n,done,ov);error=GetLastError();
    if(tracked && !result && error!=ERROR_IO_PENDING) forget(ov);
    SetLastError(error);return result;
}
static BOOL WINAPI hook_read_ex(HANDLE h,LPVOID b,DWORD n,LPOVERLAPPED ov,LPOVERLAPPED_COMPLETION_ROUTINE completion) {
    BOOL result;DWORD incoming=GetLastError(),error;int tracked=is_allowed(h);
    if(tracked) {count(CNT_READFILE_EX);if(ov)remember(h,NULL,ov,ov,b,n);}
    /* Leave the application's callback and its scheduling untouched. */
    SetLastError(incoming);result=real_read_ex(h,b,n,ov,completion);error=GetLastError();
    if(tracked && !result) forget(ov);
    SetLastError(error);return result;
}
static void result_observed(HANDLE h,LPOVERLAPPED ov,BOOL result,DWORD error,PVOID address) {
    if(!is_allowed(h))return;
    caller(2,address);
    if(result) {count(CNT_OVERLAPPED);forget(ov);}
    else if(error!=ERROR_IO_INCOMPLETE) {count(CNT_OTHER_COMPLETION);forget(ov);}
}
static BOOL WINAPI hook_result(HANDLE h,LPOVERLAPPED ov,LPDWORD done,BOOL wait) {
    BOOL r=real_result(h,ov,done,wait);DWORD e=GetLastError();
    result_observed(h,ov,r,e,__builtin_return_address(0));SetLastError(e);return r;
}
static BOOL WINAPI hook_result_ex(HANDLE h,LPOVERLAPPED ov,LPDWORD done,DWORD timeout,BOOL alertable) {
    BOOL r=real_result_ex(h,ov,done,timeout,alertable);DWORD e=GetLastError();
    result_observed(h,ov,r,(!r && (e==WAIT_TIMEOUT || e==WAIT_IO_COMPLETION))?ERROR_IO_INCOMPLETE:e,__builtin_return_address(0));
    SetLastError(e);
    return r;
}
static BOOL WINAPI hook_iocp(HANDLE port,LPDWORD done,PULONG_PTR key,LPOVERLAPPED *ov,DWORD timeout) {
    BOOL r=real_iocp(port,done,key,ov,timeout);DWORD e=GetLastError();
    if(InterlockedCompareExchange(&enabled,0,0) && ov && *ov && forget(*ov)) {count(CNT_IOCP);caller(3,__builtin_return_address(0));}
    SetLastError(e);return r;
}
static NTSTATUS NTAPI hook_nt_wait(HANDLE h,BOOLEAN alertable,PLARGE_INTEGER timeout) {
    NTSTATUS r=real_nt_wait(h,alertable,timeout);DWORD error=GetLastError();
    if(r==STATUS_SUCCESS_VALUE && InterlockedCompareExchange(&enabled,0,0) && known_event(h)) {count(CNT_WAIT);caller(6,__builtin_return_address(0));}
    SetLastError(error);return r;
}
static BOOLEAN WINAPI hook_input(HANDLE h,PVOID buffer,ULONG length) {
    BOOLEAN r;DWORD incoming=GetLastError(),e;int tracked=is_allowed(h);
    if(tracked){count(CNT_HID_INPUT);caller(4,__builtin_return_address(0));}
    SetLastError(incoming);r=real_input(h,buffer,length);e=GetLastError();SetLastError(e);return r;
}
static BOOLEAN WINAPI hook_preparsed(HANDLE h,PHIDP_PREPARSED_DATA *out) {
    BOOLEAN r=real_preparsed(h,out);DWORD e=GetLastError();unsigned i;
    if(r && out && *out && is_allowed(h)) {
        AcquireSRWLockExclusive(&data_lock);
        for(i=0;i<MAX_PARSED;i++)if(!parsed[i].data || parsed[i].data==*out){parsed[i].handle=h;parsed[i].data=*out;break;}
        ReleaseSRWLockExclusive(&data_lock);
    }
    SetLastError(e);return r;
}
static BOOLEAN WINAPI hook_free_preparsed(PHIDP_PREPARSED_DATA data) {
    BOOLEAN r=real_free_preparsed(data);DWORD e=GetLastError();unsigned i;
    if(r){AcquireSRWLockExclusive(&data_lock);for(i=0;i<MAX_PARSED;i++)if(parsed[i].data==data)ZeroMemory(&parsed[i],sizeof(parsed[i]));ReleaseSRWLockExclusive(&data_lock);}
    SetLastError(e);return r;
}
static NTSTATUS WINAPI hook_usage(HIDP_REPORT_TYPE t,USAGE page,USHORT col,USAGE usage,PULONG val,PHIDP_PREPARSED_DATA p,PCHAR report,ULONG n) {
    DWORD incoming=GetLastError();
    if(t==HidP_Input && known_parse(p,report,n)){count(CNT_HIDP_PARSE);caller(5,__builtin_return_address(0));}
    SetLastError(incoming);
    return real_usage(t,page,col,usage,val,p,report,n);
}
static NTSTATUS WINAPI hook_usages(HIDP_REPORT_TYPE t,USAGE page,USHORT col,PUSAGE usages,PULONG nusage,PHIDP_PREPARSED_DATA p,PCHAR report,ULONG n) {
    DWORD incoming=GetLastError();
    if(t==HidP_Input && known_parse(p,report,n)){count(CNT_HIDP_PARSE);caller(5,__builtin_return_address(0));}
    SetLastError(incoming);
    return real_usages(t,page,col,usages,nusage,p,report,n);
}
static NTSTATUS NTAPI hook_nt_close(HANDLE h) {
    NTSTATUS r=real_nt_close(h);DWORD error=GetLastError();unsigned i;
    if(r>=0 && InterlockedCompareExchange(&enabled,0,0)){
        AcquireSRWLockExclusive(&data_lock);
        for(i=0;i<PSKB_MAX_HANDLES;i++)if(allowed[i]==h)allowed[i]=NULL;
        for(i=0;i<MAX_PENDING;i++)if(pending[i].handle==h)ZeroMemory(&pending[i],sizeof(pending[i]));
        for(i=0;i<MAX_PARSED;i++)if(parsed[i].handle==h)ZeroMemory(&parsed[i],sizeof(parsed[i]));
        ReleaseSRWLockExclusive(&data_lock);
    }
    SetLastError(error);return r;
}
static DWORD WINAPI watchdog(LPVOID unused) {
    (void)unused;
    for(;;) {
        LONG command=InterlockedCompareExchange(&shared->command,0,0);
        LONG64 beat=InterlockedCompareExchange64(&shared->heartbeat,0,0);
        ULONGLONG now=GetTickCount64();
        if(command!=0 || beat<0 || (ULONGLONG)beat>now || now-(ULONGLONG)beat>3000)break;
        Sleep(50);
    }
    InterlockedExchange(&enabled,0);
    if(MH_DisableHook(MH_ALL_HOOKS)!=MH_OK) {InterlockedExchange(&shared->error,ERROR_GEN_FAILURE);InterlockedExchange(&shared->state,3);}
    else InterlockedExchange(&shared->state,4);
    /* Keep the DLL, mapping and trampolines alive. In-flight callers may still
     * return through a trampoline; there is deliberately no remote unload. */
    return 0;
}
static int install(HMODULE module,const char *name,LPVOID detour,LPVOID *original) {
    LPVOID target=(LPVOID)(uintptr_t)GetProcAddress(module,name);MH_STATUS status;
    if(!target)return ERROR_PROC_NOT_FOUND;
    status=MH_CreateHook(target,detour,original);if(status!=MH_OK)return 1000+(int)status;
    status=MH_QueueEnableHook(target);if(status!=MH_OK)return 1000+(int)status;
    count(CNT_HOOK_COUNT);return 0;
}
#define INSTALL(module,name,hook,original) do {error=install(module,name,(LPVOID)(uintptr_t)hook,(LPVOID*)&original);if(error)goto fail;} while(0)

__declspec(dllexport) DWORD WINAPI StartProbe(LPWSTR mapping_name) {
    WCHAR path[MAX_PATH],*basename;HMODULE nt,kernel,hid,pinned;DWORD error=0;unsigned i;HANDLE thread;
    int test;
    if(InterlockedCompareExchange(&started,1,0)!=0)return ERROR_ALREADY_INITIALIZED;
    if(!GetModuleFileNameW(NULL,path,MAX_PATH))return GetLastError();
    basename=wcsrchr(path,L'\\');basename=basename?basename+1:path;
    test=lstrcmpiW(basename,L"pskb-probe-test.exe")==0;
    if(!test && lstrcmpiW(basename,L"RemotePlay.exe")!=0)return ERROR_ACCESS_DENIED;
    mapping_handle=OpenFileMappingW(FILE_MAP_READ|FILE_MAP_WRITE,FALSE,mapping_name);
    if(!mapping_handle)return GetLastError();
    shared=(PskbShared*)MapViewOfFile(mapping_handle,FILE_MAP_READ|FILE_MAP_WRITE,0,0,sizeof(PskbShared));
    if(!shared)return GetLastError();
    if(shared->magic!=PSKB_MAGIC || shared->version!=PSKB_VERSION || shared->size!=sizeof(PskbShared) ||
       shared->target_pid!=GetCurrentProcessId() || shared->handle_count>PSKB_MAX_HANDLES ||
       shared->command!=0 || (test?shared->test_mode!=1:shared->test_mode!=0)) {error=ERROR_INVALID_DATA;goto fail;}
    InterlockedExchange(&shared->state,1);
    hid=LoadLibraryW(L"hid.dll");nt=GetModuleHandleW(L"ntdll.dll");kernel=GetModuleHandleW(L"kernelbase.dll");
    if(!hid||!nt||!kernel){error=ERROR_MOD_NOT_FOUND;goto fail;}
    for(i=0;i<shared->handle_count;i++) {
        HIDD_ATTRIBUTES attributes;HANDLE h=(HANDLE)(uintptr_t)shared->handles[i];ZeroMemory(&attributes,sizeof(attributes));attributes.Size=sizeof(attributes);
        if(!h || h==INVALID_HANDLE_VALUE || (!test && (!HidD_GetAttributes(h,&attributes)||attributes.VendorID!=0x054c||attributes.ProductID!=0x05c4))) {error=ERROR_INVALID_HANDLE;goto fail;}
        allowed[i]=h;
    }
    if(!shared->handle_count){error=ERROR_NOT_FOUND;goto fail;}
    if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,(LPCWSTR)(uintptr_t)StartProbe,&pinned)){error=GetLastError();goto fail;}
    error=(DWORD)MH_Initialize();if(error!=MH_OK){error+=1000;goto fail;}
    INSTALL(nt,"NtReadFile",hook_nt_read,real_nt_read);
    INSTALL(nt,"NtClose",hook_nt_close,real_nt_close);
    INSTALL(nt,"NtWaitForSingleObject",hook_nt_wait,real_nt_wait);
    INSTALL(kernel,"ReadFile",hook_read,real_read);
    INSTALL(kernel,"ReadFileEx",hook_read_ex,real_read_ex);
    INSTALL(kernel,"GetOverlappedResult",hook_result,real_result);
    INSTALL(kernel,"GetOverlappedResultEx",hook_result_ex,real_result_ex);
    INSTALL(kernel,"GetQueuedCompletionStatus",hook_iocp,real_iocp);
    INSTALL(hid,"HidD_GetInputReport",hook_input,real_input);
    INSTALL(hid,"HidD_GetPreparsedData",hook_preparsed,real_preparsed);
    INSTALL(hid,"HidD_FreePreparsedData",hook_free_preparsed,real_free_preparsed);
    INSTALL(hid,"HidP_GetUsageValue",hook_usage,real_usage);
    INSTALL(hid,"HidP_GetUsages",hook_usages,real_usages);
    error=(DWORD)MH_ApplyQueued();if(error!=MH_OK){error+=1000;goto fail;}
    InterlockedExchange(&enabled,1);InterlockedExchange(&shared->state,2);
    thread=CreateThread(NULL,0,watchdog,NULL,0,NULL);
    if(!thread){error=GetLastError();goto fail;}
    CloseHandle(thread);return 0;
fail:
    InterlockedExchange(&enabled,0);MH_DisableHook(MH_ALL_HOOKS);
    if(shared){InterlockedExchange(&shared->error,(LONG)error);InterlockedExchange(&shared->state,3);}return error;
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved) {
    (void)reserved;if(reason==DLL_PROCESS_ATTACH)DisableThreadLibraryCalls(instance);return TRUE;
}
