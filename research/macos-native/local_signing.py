#!/usr/bin/env python3
"""Project-only local signing. Never edits trust or falls back to ad-hoc signing.

Private assets stay outside the repository in Application Support. Passwords
enter only echo-disabled PTY prompts. API diagnostics disable UI in their own
process. Only newly created keys are imported; no keys are exported.
"""
import argparse
from contextlib import contextmanager
import ctypes
import fcntl
import hashlib
import json
import os
from pathlib import Path
import plistlib
import pty
import re
import secrets
import select
import shlex
import stat
import subprocess
import sys
import tempfile
import termios
import time
from datetime import datetime, timezone

DIRECTORY = Path.home() / "Library/Application Support/PSKeyboard/Signing"
LABEL = "PSKeyboard Local Development"
BUNDLE_ID = "com.xiunlee.pskeyboard.mac-dualsense"
SMOKE_ID = "com.xiunlee.pskeyboard.signing-smoke"
REPO = Path(__file__).resolve().parents[2]


class SafeError(Exception):
    def __init__(self, message, **details):
        super().__init__(message)
        self.details = details


def run(args, timeout=30):
    try:
        result = subprocess.run([str(x) for x in args], capture_output=True, timeout=timeout)
    except subprocess.TimeoutExpired:
        raise SafeError("operation timed out", tool=Path(args[0]).name) from None
    if result.returncode:
        metadata = b"resource fork, Finder information, or similar detritus not allowed" in result.stderr
        known_diagnostics = [phrase for phrase in [
            "MAC verification failed during PKCS12 import", "User interaction is not allowed",
            "User canceled the operation", "The user name or passphrase you entered is not correct",
            "One or more parameters passed to a function were not valid", "Unknown format in import",
            "A default keychain could not be found", "The specified keychain could not be found",
            "The specified item could not be found in the keychain", "no identity found",
            "CSSMERR_TP_NOT_TRUSTED", "errSecInternalComponent"] if phrase.encode() in result.stderr]
        raise SafeError("operation failed", tool=Path(args[0]).name,
                        operation=str(args[1]) if len(args) > 1 else "",
                        returncode=result.returncode, metadataFailure=metadata,
                        diagnostic="resource fork, Finder information, or similar detritus not allowed" if metadata else "; ".join(known_diagnostics))
    return result


def write_new(path, data):
    if isinstance(data, str):
        data = data.encode()
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "wb") as output:
        output.write(data)


def write_receipt(path, data):
    """Replace public receipts, never an identity, key or password."""
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent, delete=False) as stream:
        temporary = Path(stream.name)
        json.dump(data, stream, indent=2)
        stream.write("\n")
    temporary.chmod(0o600)
    temporary.replace(path)


def private_directory():
    if DIRECTORY.is_symlink() or not DIRECTORY.is_dir():
        raise SafeError("project signing setup is missing")
    info = DIRECTORY.stat()
    if info.st_uid != os.getuid() or stat.S_IMODE(info.st_mode) != 0o700:
        raise SafeError("project signing directory must be owned by this user with mode 0700")


def private_file(path):
    info = path.lstat()
    if not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid() or stat.S_IMODE(info.st_mode) != 0o600:
        raise SafeError("project signing file has unexpected ownership or permissions", file=path.name)


def state_snapshot():
    commands = {
        "defaultKeychain": ["/usr/bin/security", "default-keychain", "-d", "user"],
        "searchList": ["/usr/bin/security", "list-keychains", "-d", "user"],
        "userTrust": ["/usr/bin/security", "dump-trust-settings"],
        "adminTrust": ["/usr/bin/security", "dump-trust-settings", "-d"],
    }
    result = {}
    for name, args in commands.items():
        item = subprocess.run(args, capture_output=True, timeout=20)
        # Keep contents in memory only: receipts contain equality and hashes.
        result[name] = (item.returncode, item.stdout, item.stderr)
    return result


def state_comparison(before, after):
    return {name: {"unchanged": before[name] == after[name],
                   "readReturnCodeBefore": before[name][0],
                   "readReturnCodeAfter": after[name][0]}
            for name in before}


def keychain_search_list():
    result = run(["/usr/bin/security", "list-keychains", "-d", "user"])
    return shlex.split(result.stdout.decode())


@contextmanager
def signing_search_path(keychain):
    """Temporarily expose only our keychain; preserve concurrent user changes."""
    keychain = str(keychain)
    original = keychain_search_list()
    added = keychain not in original
    expected = original + [keychain] if added else original
    report = {"temporarilyAppendedProjectKeychain": added,
              "projectKeychainAlreadyPresent": not added}
    try:
        if added:
            run(["/usr/bin/security", "list-keychains", "-d", "user", "-s"] + expected)
        yield report
    finally:
        current = keychain_search_list()
        report["concurrentSearchListChangesObserved"] = current != expected
        if added:
            remaining = [value for value in current if value != keychain]
            if remaining != current:
                run(["/usr/bin/security", "list-keychains", "-d", "user", "-s"] + remaining)
        final = keychain_search_list()
        report["exactOriginalSearchListRestored"] = final == original
        report["ownTemporaryEntryRemoved"] = not added or keychain not in final
        report["concurrentOtherEntriesPreserved"] = True
        if not report["ownTemporaryEntryRemoved"]:
            raise SafeError("temporary project keychain search entry could not be removed")


class KeychainAPI:
    """Import only our new identity, with keychain UI disabled in this process."""
    def __init__(self):
        self.security = ctypes.CDLL("/System/Library/Frameworks/Security.framework/Security")
        self.security.SecKeychainSetUserInteractionAllowed.argtypes = [ctypes.c_bool]
        self.security.SecKeychainSetUserInteractionAllowed.restype = ctypes.c_int32
        self.check(self.security.SecKeychainSetUserInteractionAllowed(False), "disable process keychain UI")
        self.cf = ctypes.CDLL("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")
        self.cf.CFRelease.argtypes = [ctypes.c_void_p]
        self.security.SecKeychainOpen.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_void_p)]
        self.security.SecKeychainOpen.restype = ctypes.c_int32

    @staticmethod
    def check(status, operation):
        if status:
            raise SafeError("keychain operation failed", operation=operation, osstatus=status)

    def import_identity(self, path, pkcs12, password):
        """Import our new encrypted P12 with a sign-only, nonextractable key."""
        class Parameters(ctypes.Structure):
            _fields_ = [("version", ctypes.c_uint32), ("flags", ctypes.c_uint32),
                        ("passphrase", ctypes.c_void_p), ("alertTitle", ctypes.c_void_p),
                        ("alertPrompt", ctypes.c_void_p), ("accessRef", ctypes.c_void_p),
                        ("keyUsage", ctypes.c_void_p), ("keyAttributes", ctypes.c_void_p)]
        cf = self.cf
        security = self.security
        cf.CFStringCreateWithCString.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_uint32]
        cf.CFStringCreateWithCString.restype = ctypes.c_void_p
        cf.CFDataCreate.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_long]
        cf.CFDataCreate.restype = ctypes.c_void_p
        cf.CFArrayCreate.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_void_p), ctypes.c_long, ctypes.c_void_p]
        cf.CFArrayCreate.restype = ctypes.c_void_p
        cf.CFArrayGetCount.argtypes = [ctypes.c_void_p]
        cf.CFArrayGetCount.restype = ctypes.c_long
        cf.CFArrayGetValueAtIndex.argtypes = [ctypes.c_void_p, ctypes.c_long]
        cf.CFArrayGetValueAtIndex.restype = ctypes.c_void_p
        cf.CFDictionaryGetValue.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
        cf.CFDictionaryGetValue.restype = ctypes.c_void_p
        cf.CFBooleanGetValue.argtypes = [ctypes.c_void_p]
        cf.CFBooleanGetValue.restype = ctypes.c_bool
        security.SecTrustedApplicationCreateFromPath.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_void_p)]
        security.SecTrustedApplicationCreateFromPath.restype = ctypes.c_int32
        security.SecAccessCreate.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.POINTER(ctypes.c_void_p)]
        security.SecAccessCreate.restype = ctypes.c_int32
        security.SecItemImport.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.POINTER(ctypes.c_uint32),
            ctypes.POINTER(ctypes.c_uint32), ctypes.c_uint32, ctypes.POINTER(Parameters),
            ctypes.c_void_p, ctypes.POINTER(ctypes.c_void_p)]
        security.SecItemImport.restype = ctypes.c_int32
        security.SecIdentityCopyPrivateKey.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_void_p)]
        security.SecIdentityCopyPrivateKey.restype = ctypes.c_int32
        security.SecKeyCopyAttributes.argtypes = [ctypes.c_void_p]
        security.SecKeyCopyAttributes.restype = ctypes.c_void_p
        owned = []

        def own(reference):
            pointer = reference.value if isinstance(reference, ctypes.c_void_p) else reference
            if not pointer:
                raise SafeError("Security import returned a null object")
            owned.append(pointer)
            return pointer

        def array(values):
            pointers = (ctypes.c_void_p * len(values))(*values)
            # Each referenced object stays alive until the import completes.
            return own(cf.CFArrayCreate(None, pointers, len(values), None))

        def symbol(name):
            return ctypes.c_void_p.in_dll(security, name).value

        try:
            handle = ctypes.c_void_p()
            self.check(security.SecKeychainOpen(os.fsencode(path), ctypes.byref(handle)), "open import keychain")
            own(handle)
            trusted = ctypes.c_void_p()
            self.check(security.SecTrustedApplicationCreateFromPath(b"/usr/bin/codesign", ctypes.byref(trusted)), "trusted signer")
            trusted_apps = array([own(trusted)])
            label = own(cf.CFStringCreateWithCString(None, LABEL.encode(), 0x08000100))
            access = ctypes.c_void_p()
            self.check(security.SecAccessCreate(label, trusted_apps, ctypes.byref(access)), "create signer ACL")
            own(access)
            passphrase = own(cf.CFStringCreateWithCString(None, password, 0x08000100))
            encoded = pkcs12.read_bytes()
            buffer = ctypes.create_string_buffer(encoded)
            data = own(cf.CFDataCreate(None, buffer, len(encoded)))
            parameters = Parameters(0, 1, passphrase, None, None, access,
                array([symbol("kSecAttrCanSign")]),
                array([symbol("kSecAttrIsPermanent"), symbol("kSecAttrIsSensitive")]))
            format_value = ctypes.c_uint32(12)  # kSecFormatPKCS12, SDK SecImportExport.h
            item_type = ctypes.c_uint32(5)       # kSecItemTypeAggregate
            items = ctypes.c_void_p()
            self.check(security.SecItemImport(data, None, ctypes.byref(format_value), ctypes.byref(item_type),
                       0, ctypes.byref(parameters), handle, ctypes.byref(items)), "import new identity")
            own(items)
            if cf.CFArrayGetCount(items) != 1:
                raise SafeError("expected exactly one imported identity")
            identity = cf.CFArrayGetValueAtIndex(items, 0)
            private_key = ctypes.c_void_p()
            self.check(security.SecIdentityCopyPrivateKey(identity, ctypes.byref(private_key)), "inspect new key attributes")
            own(private_key)
            attributes = own(security.SecKeyCopyAttributes(private_key))
            observed = {}
            for key in ["kSecAttrIsExtractable", "kSecAttrIsSensitive", "kSecAttrIsPermanent", "kSecAttrCanSign"]:
                value = cf.CFDictionaryGetValue(attributes, symbol(key))
                observed[key] = bool(cf.CFBooleanGetValue(value)) if value else None
            if observed["kSecAttrIsExtractable"] is not False or observed["kSecAttrCanSign"] is not True:
                raise SafeError("new key attributes did not match request", attributes=observed)
            return observed
        finally:
            for reference in reversed(owned):
                cf.CFRelease(reference)


def password_command(arguments, password, expected_prompts=1):
    """Send only our generated password, to an observed echo-disabled prompt."""
    pid, master = pty.fork()
    if pid == 0:
        os.execv("/usr/bin/security", ["security"] + [str(value) for value in arguments])
    transcript = bytearray()
    sent = 0
    consumed = 0
    deadline = time.monotonic() + 15
    completed = False
    try:
        while time.monotonic() < deadline:
            ready, _, _ = select.select([master], [], [], 0.05)
            if ready:
                try:
                    chunk = os.read(master, 16384)
                except OSError:
                    chunk = b""
                if chunk:
                    transcript.extend(chunk)
                    if len(transcript) > 262144:
                        raise SafeError("partition command exceeded output limit")
                    recent = transcript[consumed:]
                    recognized = any(prompt in recent for prompt in
                                     [b"password to unlock", b"password for new keychain:",
                                      b"retype password for new keychain:"])
                    if sent < expected_prompts and recognized and recent.rstrip().endswith(b":"):
                        if termios.tcgetattr(master)[3] & termios.ECHO:
                            raise SafeError("password prompt did not disable terminal echo")
                        os.write(master, password + b"\n")
                        sent += 1
                        consumed = len(transcript)
            exited, status = os.waitpid(pid, os.WNOHANG)
            if exited:
                completed = True
                if sent != expected_prompts or not os.WIFEXITED(status) or os.WEXITSTATUS(status) != 0:
                    raise SafeError("project keychain command failed", operation=arguments[0],
                                    passwordPromptsAnswered=sent)
                return
        raise SafeError("project keychain command timed out", operation=arguments[0])
    finally:
        if not completed:
            try:
                os.kill(pid, 15)
                os.waitpid(pid, 0)
            except ProcessLookupError:
                pass
        os.close(master)
        # Never emit child output; Security tools may include key attributes.
        transcript[:] = b"\0" * len(transcript)


def set_partition(keychain, password):
    """Only our new, precisely labelled private key. No password in argv/logs."""
    password_command(["set-key-partition-list", "-S", "apple:", "-l", LABEL,
                      "-s", "-t", "private", keychain], password)


def load_identity():
    private_directory()
    metadata_path = DIRECTORY / "identity.json"
    password_path = DIRECTORY / "keychain-password"
    for path in [metadata_path, password_path]:
        private_file(path)
    data = json.loads(metadata_path.read_text())
    keychain = Path(data["keychain"])
    if keychain.parent != DIRECTORY or keychain.name != "PSKeyboard-Local.keychain-db":
        raise SafeError("unexpected project keychain location")
    private_file(keychain)
    if not re.fullmatch(r"[0-9A-F]{40}", data["certificateSHA1"]):
        raise SafeError("invalid public certificate fingerprint")
    password = password_path.read_bytes().strip()
    if not re.fullmatch(rb"[0-9a-f]{64}", password):
        raise SafeError("invalid project keychain password format")
    return data, password


def designated_requirement(path):
    result = run(["/usr/bin/codesign", "--display", "-r", "-", path])
    text = (result.stdout + result.stderr).decode("utf-8", errors="replace")
    match = re.search(r"(?m)^(?:#\s*)?designated => (.+)$", text)
    if not match:
        raise SafeError("codesign did not return a designated requirement")
    return match.group(1)


def sign_code(path, identity, identifier, entitlements=None):
    command = ["/usr/bin/codesign", "--force", "--sign", identity["certificateSHA1"],
               "--keychain", identity["keychain"], "--timestamp=none", "--identifier", identifier]
    if entitlements is not None:
        command += ["--entitlements", entitlements]
    run(command + [path])
    requirement = 'identifier "' + identifier + '" and certificate leaf = H"' + identity["certificateSHA1"] + '"'
    run(["/usr/bin/codesign", "--verify", "--strict", "--verbose=2", "-R", "=" + requirement, path])
    return designated_requirement(path)


def smoke(identity):
    with tempfile.TemporaryDirectory(prefix="smoke-", dir=DIRECTORY) as folder:
        folder = Path(folder)
        binaries = []
        requirements = []
        hashes = []
        for version in [1, 2]:
            source = folder / ("version" + str(version) + ".c")
            binary = source.with_suffix("")
            write_new(source, "int main(void) { volatile int version = " + str(version) + "; return version - " + str(version) + "; }\n")
            run(["/usr/bin/xcrun", "clang", "-O0", source, "-o", binary], timeout=60)
            binary.chmod(0o700)
            requirements.append(sign_code(binary, identity, SMOKE_ID))
            hashes.append(hashlib.sha256(binary.read_bytes()).hexdigest())
            run([binary])
            binaries.append(binary)
        run(["/usr/bin/codesign", "--verify", "--strict", "-R", "=" + requirements[0], binaries[1]])
        run(["/usr/bin/codesign", "--verify", "--strict", "-R", "=" + requirements[1], binaries[0]])
        if requirements[0] != requirements[1] or hashes[0] == hashes[1] or "cdhash" in requirements[0]:
            raise SafeError("two-version stable identity test failed")
        return {"binariesRanWithExit0": 2, "differentExecutableHashes": True,
                "identicalDesignatedRequirements": True, "mutualRequirementChecksPassed": True,
                "designatedRequirement": requirements[0], "binarySHA256": hashes,
                "temporaryBinariesRemoved": True, "accessibilityPersistenceTested": False}


def setup():
    if DIRECTORY.exists() or DIRECTORY.is_symlink():
        raise SafeError("setup refuses to overwrite an existing signing directory or identity")
    for parent in [DIRECTORY.parent, DIRECTORY.parent.parent]:
        if parent.is_symlink():
            raise SafeError("signing parent must not be a symlink")
    DIRECTORY.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    DIRECTORY.mkdir(mode=0o700)
    before = state_snapshot()
    keychain = None
    try:
        password = secrets.token_hex(32).encode()
        write_new(DIRECTORY / "keychain-password", password + b"\n")
        with tempfile.TemporaryDirectory(prefix="bootstrap-", dir=DIRECTORY) as temporary:
            temporary = Path(temporary)
            pem_password = temporary / "pem-password"
            write_new(pem_password, secrets.token_hex(32) + "\n")
            config = temporary / "certificate.cnf"
            write_new(config, "[req]\nprompt=no\ndistinguished_name=dn\nx509_extensions=signing\n"
                      "[dn]\nCN=" + LABEL + "\n[signing]\nbasicConstraints=critical,CA:FALSE\n"
                      "keyUsage=critical,digitalSignature\nextendedKeyUsage=critical,codeSigning\nsubjectKeyIdentifier=hash\n")
            certificate = DIRECTORY / "certificate.pem"
            private_key = temporary / "new-private-key.pem"
            run(["/usr/bin/openssl", "req", "-new", "-newkey", "rsa:3072", "-x509", "-sha256",
                 "-days", "3650", "-config", config, "-keyout", private_key, "-out", certificate,
                 "-passout", "file:" + str(pem_password)], timeout=60)
            certificate.chmod(0o600)
            private_key.chmod(0o600)
            der = run(["/usr/bin/openssl", "x509", "-in", certificate, "-outform", "DER"]).stdout
            fingerprint = hashlib.sha1(der).hexdigest().upper()
            pkcs12 = temporary / "one-time-import.p12"
            p12_password = temporary / "p12-password"
            write_new(p12_password, secrets.token_hex(32) + "\n")
            run(["/usr/bin/openssl", "pkcs12", "-export", "-inkey", private_key, "-in", certificate,
                 "-name", LABEL, "-passin", "file:" + str(pem_password),
                 "-passout", "file:" + str(p12_password), "-out", pkcs12])
            pkcs12.chmod(0o600)
            keychain = DIRECTORY / "PSKeyboard-Local.keychain-db"
            password_command(["create-keychain", keychain], password, expected_prompts=2)
            keychain.chmod(0o600)
            password_command(["unlock-keychain", keychain], password)
            run(["/usr/bin/security", "set-keychain-settings", "-l", "-u", "-t", "300", keychain])
            try:
                key_attributes = KeychainAPI().import_identity(keychain, pkcs12, p12_password.read_bytes().strip())
            finally:
                pkcs12.unlink(missing_ok=True)
            set_partition(keychain, password)
            identity = {"schemaVersion": 1, "label": LABEL, "keychain": str(keychain),
                        "certificate": str(certificate), "certificateSHA1": fingerprint,
                        "certificateSHA256": hashlib.sha256(der).hexdigest(),
                        "bundleIdentifier": BUNDLE_ID, "createdAtUTC": datetime.now(timezone.utc).isoformat(),
                        "privateKeyImportedNonExtractable": True,
                        "observedKeyAttributes": key_attributes,
                        "trustedApplicationACL": ["/usr/bin/codesign"], "partitionList": ["apple:"],
                        "trustSettingsChangedByIdentityCreation": False, "appleDeveloperIdentity": False}
            write_new(DIRECTORY / "identity.json", json.dumps(identity, indent=2) + "\n")
        with signing_search_path(keychain) as search_path:
            test = smoke(identity)
        after = state_snapshot()
        unchanged = state_comparison(before, after)
        if not all(value["unchanged"] for value in unchanged.values()):
            raise SafeError("system signing preferences changed unexpectedly", comparison=unchanged)
        result = {"ok": True, "action": "setup", "identity": identity, "test": test,
                  "systemState": unchanged, "formalAppModified": False,
                  "searchListIsolation": search_path,
                  "existingKeysExported": False, "privateBootstrapFilesRemoved": True}
        write_new(DIRECTORY / "setup-receipt.json", json.dumps(result, indent=2) + "\n")
        receipt = REPO / "research/macos-native/inspection/LOCAL-SIGNING-SETUP.json"
        write_new(receipt, json.dumps(result, indent=2) + "\n")
        return result
    finally:
        if keychain is not None:
            subprocess.run(["/usr/bin/security", "lock-keychain", str(keychain)], capture_output=True, timeout=20)


def existing(action, path=None):
    private_directory()
    lock_path = DIRECTORY / "operation.lock"
    fd = os.open(lock_path, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "rb") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        identity, password = load_identity()
        keychain = identity["keychain"]
        password_command(["unlock-keychain", keychain], password)
        if action == "unlock":
            return {"ok": True, "action": "unlock", "keychain": keychain, "autoLockSeconds": 300}
        try:
            before = state_snapshot()
            with signing_search_path(keychain) as search_path:
                if action == "check":
                    result = {"ok": True, "action": "check", "identity": identity, "test": smoke(identity),
                              "formalAppModified": False, "existingKeysExported": False,
                              "privateBootstrapFilesRemoved": True}
                else:
                    app = Path(path).absolute()
                    if app.is_symlink() or not app.is_dir() or app.suffix != ".app":
                        raise SafeError("sign requires a local app bundle")
                    info = plistlib.loads((app / "Contents/Info.plist").read_bytes())
                    if info.get("CFBundleIdentifier") != BUNDLE_ID:
                        raise SafeError("refusing to sign a different application's bundle")
                    helpers = app / "Contents/Helpers"
                    if helpers.exists():
                        allowed = {"PSKeyboardRuntime", "PSKeyboardGuest"}
                        if {item.name for item in helpers.iterdir()} != allowed:
                            raise SafeError("unexpected project helper bundle contents")
                        with tempfile.TemporaryDirectory(prefix="helper-sign-", dir=DIRECTORY) as temporary:
                            entitlement = Path(temporary) / "virtualization.plist"
                            entitlement.write_bytes(plistlib.dumps({"com.apple.security.virtualization": True}))
                            for name in sorted(allowed):
                                helper = helpers / name
                                if helper.is_symlink() or not helper.is_file():
                                    raise SafeError("invalid project helper")
                                sign_code(helper, identity, BUNDLE_ID + "." + name,
                                          entitlement if name == "PSKeyboardGuest" else None)
                    requirement = sign_code(app, identity, BUNDLE_ID)
                    result = {"ok": True, "action": "sign", "path": str(app),
                              "certificateSHA1": identity["certificateSHA1"], "designatedRequirement": requirement,
                              "strictVerificationPassed": True}
            result["systemState"] = state_comparison(before, state_snapshot())
            result["searchListIsolation"] = search_path
            if action == "check":
                write_receipt(DIRECTORY / "setup-receipt.json", result)
                write_receipt(REPO / "research/macos-native/inspection/LOCAL-SIGNING-SETUP.json", result)
            return result
        finally:
            subprocess.run(["/usr/bin/security", "lock-keychain", keychain], capture_output=True, timeout=20)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="action", required=True)
    commands.add_parser("setup", help="Create a new project identity; refuses any existing setup")
    commands.add_parser("unlock", help="Unlock only the existing project keychain")
    commands.add_parser("check", help="Compile/sign/run two harmless version probes and compare DRs")
    signing = commands.add_parser("sign", help="Sign only this project's app using the existing identity")
    signing.add_argument("--path", required=True)
    args = parser.parse_args()
    os.umask(0o077)
    try:
        result = setup() if args.action == "setup" else existing(args.action, getattr(args, "path", None))
    except SafeError as error:
        print(json.dumps({"ok": False, "error": str(error), **error.details}))
        return 1
    except Exception as error:
        # Do not print arbitrary exception text: it could contain input secrets.
        print(json.dumps({"ok": False, "error": "local signing operation failed", "exceptionType": type(error).__name__}))
        return 1
    print(json.dumps(result, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
