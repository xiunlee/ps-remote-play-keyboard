# Building the public alpha from source

The source asset on the same GitHub Release page contains the exact project
snapshot, a pinned upstream VIIPER source ZIP, the backend wrapper's module files,
and the complete source of the three Go modules linked into the backend. See
`SOURCE.json` and `SHA256SUMS.txt` in that asset for identities. This is not an
offline toolchain bundle: compiler downloads and Go module resolution may need
network access. No Sony account, controller, driver operation or payment is needed
to compile or run the offline tests.

## Prerequisites

- Windows x64 with PowerShell 7, Git and the .NET Framework 4.x C# compiler.
- Go **1.27.3 windows/amd64** from <https://go.dev/dl/>.
- Zig **0.17.0 x86_64-windows** from <https://ziglang.org/download/>.
- Extract the matching source asset from the release page. Compiler binaries are
  not included; their license materials are retained in the runtime package.

The candidate build records the Git commit and requires a clean checkout.
An extracted source ZIP has no `.git` directory. Clone the repository and check
out the exact tag or the full commit named in the source asset's `SOURCE.json`:

```powershell
git clone https://github.com/xiunlee/ps-remote-play-keyboard.git
Set-Location ps-remote-play-keyboard
git checkout --detach v0.1.0-alpha.12
```

Use the supplied `dependencies/VIIPER-source.zip`. It must have SHA-256
`8305750E881E7E955DCF26DC5FEDC2B43D55902C9F1FA5B4ECA31FE51901B2A5`.
The build verifies it and extracts the source into a new isolated build directory.
The original upstream files are not patched. Our wrapper and module files control
the restricted server entry point. The reference `dependencies/wrapper/go.mod`
uses a relative replacement path; to inspect/build it directly, first extract
`VIIPER-source.zip` into `dependencies`.

```powershell
./research/dualsense-preview/backend/Build-Backend.ps1 `
  -SourceArchive <source-asset>/dependencies/VIIPER-source.zip `
  -GoExecutable <go-toolchain>/bin/go.exe `
  -OutputDirectory <new-backend-directory>

./research/dualsense-preview/Build-Candidate.ps1 `
  -BackendDirectory <new-backend-directory> `
  -Zig <zig-toolchain>/zig.exe `
  -OutputDirectory <new-player-directory> -RunTests -RequireClean

./research/dualsense-preview/Verify-Candidate.ps1 `
  -BuildDirectory <new-player-directory> -RequireClean
```

Replace the angle-bracket placeholders with your paths. Use a new output directory
each time. The native mapping DLL is built from `research/native-merge` source;
no old PSKeyboard installation or binary is required. Both the native tests and
the .NET interop test run without live keyboard, USB or Sony access.

The backend build pins the upstream commit and `go.sum`. Included Go dependency
sources correspond to the modules listed in `MATERIALS.json`; the build normally
resolves those exact versions through the Go module proxy and checksum database.
The archive does not promise an offline rebuild or bit-identical output across
different compilers, paths, build times or modified sources. Build manifests record
the actual source and output hashes.

To modify the program, create your own branch, change the source and increase
`VERSION` before making a new build. Existing version tags are immutable. Keep the
GPL and third-party notices when distributing your modified version, identify your
changes and provide the corresponding source. Optional sponsorship links should
not be presented as belonging to the original author if you change their recipient.

## Preparing matching release assets

From the clean checkout used for the player build:

```powershell
./packaging/Build-PublicRelease.ps1 `
  -BuildDirectory <new-player-directory> `
  -BackendDirectory <new-backend-directory> `
  -UpstreamArchive <source-asset>/dependencies/VIIPER-source.zip `
  -OutputDirectory <new-package-directory>
```

Publish the resulting Windows ZIP, corresponding source ZIP, `RELEASE.json` and
`SHA256SUMS.txt` together on the same release page. Packaging copies an explicit
runtime file list, excluding personal settings, session diagnostics and payment
data. The scripts only build, test and package; they do not publish or launch the
normal GUI or backend, install drivers, or control Sony Remote Play.
