#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winternl.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <wchar.h>
#include "shared.h"

typedef DWORD (WINAPI *StartFn)(LPWSTR);
typedef NTSTATUS (NTAPI *RawReadFn)(HANDLE,HANDLE,PVOID,PVOID,PIO_STATUS_BLOCK,PVOID,ULONG,PLARGE_INTEGER,PULONG);
static unsigned failures;
static volatile LONG apc_seen;
static VOID CALLBACK mark_apc(ULONG_PTR unused) {(void)unused;InterlockedIncrement(&apc_seen);}
#define CHECK(expr,label) do {if(!(expr)){printf("FAIL %s (error=%lu)\n",label,GetLastError());failures++;}else printf("PASS %s\n",label);}while(0)

static HANDLE temporary_file(const unsigned char *bytes) {
    WCHAR dir[MAX_PATH],path[MAX_PATH];HANDLE h;DWORD done;
    if(!GetTempPathW(MAX_PATH,dir)||!GetTempFileNameW(dir,L"pkb",0,path))return INVALID_HANDLE_VALUE;
    h=CreateFileW(path,GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_EXISTING,
        FILE_ATTRIBUTE_TEMPORARY|FILE_FLAG_DELETE_ON_CLOSE,NULL);
    if(h==INVALID_HANDLE_VALUE)return h;
    if(!WriteFile(h,bytes,64,&done,NULL)||done!=64){CloseHandle(h);return INVALID_HANDLE_VALUE;}
    SetFilePointer(h,0,NULL,FILE_BEGIN);return h;
}
static BOOL make_pipe(unsigned id,HANDLE *server,HANDLE *client) {
    WCHAR name[128];OVERLAPPED ov;BOOL ok;DWORD err;
    swprintf(name,128,L"\\\\.\\pipe\\pskb-probe-test-%lu-%u",GetCurrentProcessId(),id);
    *server=CreateNamedPipeW(name,PIPE_ACCESS_INBOUND|FILE_FLAG_OVERLAPPED,PIPE_TYPE_BYTE|PIPE_READMODE_BYTE|PIPE_WAIT,1,4096,4096,0,NULL);
    if(*server==INVALID_HANDLE_VALUE)return FALSE;
    *client=CreateFileW(name,GENERIC_WRITE,0,NULL,OPEN_EXISTING,0,NULL);
    if(*client==INVALID_HANDLE_VALUE)return FALSE;
    ZeroMemory(&ov,sizeof(ov));ov.hEvent=CreateEventW(NULL,TRUE,FALSE,NULL);
    ok=ConnectNamedPipe(*server,&ov);err=GetLastError();
    if(!ok && err==ERROR_IO_PENDING) {DWORD done;ok=GetOverlappedResult(*server,&ov,&done,TRUE);}
    else if(!ok && err==ERROR_PIPE_CONNECTED)ok=TRUE;
    CloseHandle(ov.hEvent);return ok;
}
static void beat(PskbShared *s){InterlockedExchange64(&s->heartbeat,(LONG64)GetTickCount64());}
static int attach_host(HANDLE file,const unsigned char *expected) {
    unsigned char buffer[64];DWORD done;ULONGLONG until=GetTickCount64()+45000;unsigned reads=0;
    printf("PID=%lu HANDLE=0x%llX MODE=0x20\n",GetCurrentProcessId(),(unsigned long long)(uintptr_t)file);fflush(stdout);
    while(GetTickCount64()<until) {
        SetFilePointer(file,0,NULL,FILE_BEGIN);ZeroMemory(buffer,sizeof(buffer));
        if(!ReadFile(file,buffer,64,&done,NULL)||done!=64||memcmp(buffer,expected,64)!=0){puts("ATTACH_HOST_DATA_FAILURE");return 1;}
        reads++;Sleep(10);
    }
    printf("ATTACH_HOST_PASS reads=%u payload_unchanged=1\n",reads);return 0;
}
int main(int argc,char **argv) {
    unsigned char expected[64],buffer[64];unsigned i;DWORD done,error,result,baseline_read_error,baseline_nt_error;BOOL ok;
    HANDLE file,other,server1,client1,server2,client2,port,map;HMODULE dll;StartFn start;
    WCHAR mapname[128];PskbShared *s;OVERLAPPED ov;LPOVERLAPPED returned;ULONG_PTR key;
    LONG before_read,before_nt,before_completion;ULONGLONG deadline;
    RawReadFn raw_read;IO_STATUS_BLOCK iosb;NTSTATUS nt_result;
    const DWORD sentinel=0x5a17c0de;
    for(i=0;i<64;i++)expected[i]=(unsigned char)(i*3+1);
    file=temporary_file(expected);if(file==INVALID_HANDLE_VALUE){puts("File setup failed");return 1;}
    if(argc>1 && strcmp(argv[1],"--wait-for-attach")==0)return attach_host(file,expected);
    if(argc!=2){puts("Usage: pskb-probe-test.exe <absolute-probe-dll> | --wait-for-attach");return 2;}
    other=temporary_file(expected);
    if(other==INVALID_HANDLE_VALUE||!make_pipe(1,&server1,&client1)||!make_pipe(2,&server2,&client2)){puts("I/O setup failed");return 1;}
    port=CreateIoCompletionPort(server2,NULL,0x1234,0);if(!port){puts("IOCP setup failed");return 1;}
    raw_read=(RawReadFn)(uintptr_t)GetProcAddress(GetModuleHandleW(L"ntdll.dll"),"NtReadFile");
    if(!raw_read){puts("Missing native read");return 1;}
    SetFilePointer(file,0,NULL,FILE_BEGIN);SetLastError(sentinel);
    ok=ReadFile(file,buffer,64,&done,NULL);baseline_read_error=GetLastError();
    CHECK(ok && done==64,"capture unhooked ReadFile success baseline");
    SetFilePointer(file,0,NULL,FILE_BEGIN);ZeroMemory(&iosb,sizeof(iosb));SetLastError(sentinel);
    nt_result=raw_read(file,NULL,NULL,NULL,&iosb,buffer,64,NULL,NULL);baseline_nt_error=GetLastError();
    CHECK(nt_result==0 && iosb.Information==64,"capture unhooked NtReadFile success baseline");
    swprintf(mapname,128,L"Local\\PSKB-ProbeTest-%lu",GetCurrentProcessId());
    map=CreateFileMappingW(INVALID_HANDLE_VALUE,NULL,PAGE_READWRITE,0,sizeof(PskbShared),mapname);
    s=(PskbShared*)MapViewOfFile(map,FILE_MAP_ALL_ACCESS,0,0,sizeof(PskbShared));if(!s){puts("Mapping failed");return 1;}
    ZeroMemory(s,sizeof(*s));s->magic=PSKB_MAGIC;s->version=PSKB_VERSION;s->size=sizeof(*s);s->target_pid=GetCurrentProcessId();s->test_mode=1;
    s->handle_count=3;s->handles[0]=(uint64_t)(uintptr_t)file;s->handles[1]=(uint64_t)(uintptr_t)server1;s->handles[2]=(uint64_t)(uintptr_t)server2;
    s->handle_mode[0]=0x20;beat(s);
    dll=LoadLibraryA(argv[1]);if(!dll){printf("DLL load failed %lu\n",GetLastError());return 1;}
    start=(StartFn)(uintptr_t)GetProcAddress(dll,"StartProbe");if(!start){puts("Missing StartProbe");return 1;}
    result=start(mapname);CHECK(result==0 && s->state==2,"start observation");if(result!=0)return 1;
    CHECK(s->counts[CNT_HOOK_COUNT]>=8,"expected hooks installed");

    SetFilePointer(file,0,NULL,FILE_BEGIN);ZeroMemory(buffer,sizeof(buffer));SetLastError(sentinel);
    ok=ReadFile(file,buffer,64,&done,NULL);error=GetLastError();
    CHECK(ok && done==64 && memcmp(buffer,expected,64)==0,"synchronous payload unchanged");
    CHECK(error==baseline_read_error,"ReadFile success LastError matches unhooked baseline");
    CHECK(s->counts[CNT_READFILE]>0 && s->counts[CNT_NT_READ]>0 && s->counts[CNT_NT_SUCCESS]>0,"synchronous path observed");
    SetFilePointer(file,0,NULL,FILE_BEGIN);ZeroMemory(&iosb,sizeof(iosb));ZeroMemory(buffer,sizeof(buffer));SetLastError(sentinel);
    nt_result=raw_read(file,NULL,NULL,NULL,&iosb,buffer,64,NULL,NULL);error=GetLastError();
    CHECK(nt_result==0 && iosb.Information==64 && memcmp(buffer,expected,64)==0,"native synchronous payload unchanged");
    CHECK(error==baseline_nt_error,"NtReadFile success LastError matches unhooked baseline");
    before_read=s->counts[CNT_READFILE];before_nt=s->counts[CNT_NT_READ];
    ok=ReadFile(other,buffer,64,&done,NULL);
    CHECK(ok && done==64 && memcmp(buffer,expected,64)==0,"unlisted file unaffected");
    CHECK(s->counts[CNT_READFILE]==before_read && s->counts[CNT_NT_READ]==before_nt,"unlisted file not counted");

    beat(s);ZeroMemory(&ov,sizeof(ov));ov.hEvent=CreateEventW(NULL,TRUE,FALSE,NULL);ZeroMemory(buffer,sizeof(buffer));
    ok=ReadFile(server1,buffer,64,NULL,&ov);error=GetLastError();
    CHECK(!ok && error==ERROR_IO_PENDING,"overlapped read remains pending");
    ok=GetOverlappedResult(server1,&ov,&done,FALSE);error=GetLastError();
    CHECK(!ok && error==ERROR_IO_INCOMPLETE,"pending status preserved");
    ok=GetOverlappedResultEx(server1,&ov,&done,1,FALSE);error=GetLastError();
    CHECK(!ok && error==WAIT_TIMEOUT,"overlapped timeout preserved");
    before_completion=s->counts[CNT_OTHER_COMPLETION];
    CHECK(QueueUserAPC(mark_apc,GetCurrentThread(),0)!=0,"alertable wait test APC queued");
    ok=GetOverlappedResultEx(server1,&ov,&done,100,TRUE);error=GetLastError();
    CHECK(!ok && error==WAIT_IO_COMPLETION && apc_seen==1,"alertable wait APC result preserved");
    CHECK(s->counts[CNT_OTHER_COMPLETION]==before_completion,"APC interruption is not a completed I/O");
    CHECK(WriteFile(client1,expected,64,&done,NULL) && done==64,"test data delivered");
    ok=GetOverlappedResult(server1,&ov,&done,TRUE);
    CHECK(ok && done==64 && memcmp(buffer,expected,64)==0,"event completion payload unchanged");
    CHECK(s->counts[CNT_NT_PENDING]>0 && s->counts[CNT_OVERLAPPED]>0,"event completion observed");

    beat(s);ResetEvent(ov.hEvent);ZeroMemory(buffer,sizeof(buffer));
    before_completion=s->counts[CNT_OTHER_COMPLETION];
    ok=ReadFile(server1,buffer,64,NULL,&ov);error=GetLastError();
    CHECK(!ok && error==ERROR_IO_PENDING,"cancel test submitted");
    CHECK(CancelIoEx(server1,&ov),"cancel request accepted");
    ok=GetOverlappedResult(server1,&ov,&done,TRUE);error=GetLastError();
    CHECK(!ok && error==ERROR_OPERATION_ABORTED,"cancellation preserved");
    CHECK(s->counts[CNT_OTHER_COMPLETION]>before_completion,"cancellation completion observed");
    CloseHandle(ov.hEvent);

    beat(s);ZeroMemory(&ov,sizeof(ov));ZeroMemory(buffer,sizeof(buffer));
    ok=ReadFile(server2,buffer,64,NULL,&ov);error=GetLastError();
    CHECK(!ok && error==ERROR_IO_PENDING,"IOCP read submitted");
    CHECK(WriteFile(client2,expected,64,&done,NULL) && done==64,"IOCP test data delivered");
    returned=NULL;key=0;ok=GetQueuedCompletionStatus(port,&done,&key,&returned,1000);
    CHECK(ok && returned==&ov && key==0x1234 && done==64 && memcmp(buffer,expected,64)==0,"IOCP completion unchanged");
    CHECK(s->counts[CNT_IOCP]>0,"IOCP completion observed");
    CHECK(s->counts[CNT_VALID_REPORT]==0 && s->counts[CNT_MERGED]==0,"observation never inspects or modifies reports");

    InterlockedExchange(&s->command,2);deadline=GetTickCount64()+2000;
    while(s->state==2 && GetTickCount64()<deadline)Sleep(10);
    CHECK(s->state==4,"stop acknowledged");
    before_read=s->counts[CNT_READFILE];before_nt=s->counts[CNT_NT_READ];
    SetFilePointer(file,0,NULL,FILE_BEGIN);ok=ReadFile(file,buffer,64,&done,NULL);
    CHECK(ok && done==64 && memcmp(buffer,expected,64)==0,"post-stop payload unchanged");
    CHECK(s->counts[CNT_READFILE]==before_read && s->counts[CNT_NT_READ]==before_nt,"hooks disabled after stop");
    printf("RESULT failures=%u nt=%ld pending=%ld overlapped=%ld iocp=%ld waits=%ld hooks=%ld\n",failures,
        s->counts[CNT_NT_READ],s->counts[CNT_NT_PENDING],s->counts[CNT_OVERLAPPED],s->counts[CNT_IOCP],s->counts[CNT_WAIT],s->counts[CNT_HOOK_COUNT]);
    CloseHandle(file);CloseHandle(other);CloseHandle(client1);CloseHandle(server1);CloseHandle(client2);CloseHandle(server2);CloseHandle(port);
    /* The pinned probe retains the shared mapping until this process exits. */
    return failures?1:0;
}
