#!/usr/bin/env python3
# Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
"""Bounded neutral-only trial of an already verified, running OJD service.
Does not start/stop the service, grant permissions, send buttons, or touch Sony.
Requires an empty gamepad baseline. No automatic retries or broad device cleanup.
"""
import argparse, datetime, json, pathlib, subprocess, time
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--profile', choices=['generic','sdl-compatible','game-controller-compatible'], required=True)
parser.add_argument('--output', type=pathlib.Path, required=True)
parser.add_argument('--cli', type=pathlib.Path, required=True)
parser.add_argument('--inspector', type=pathlib.Path, required=True)
a=parser.parse_args()
a.output.mkdir(parents=True,exist_ok=False)
def snapshot():
    return json.loads(subprocess.check_output([str(a.inspector.resolve())],text=True,timeout=5))
before=snapshot()
if before: raise SystemExit('Expected empty gamepad baseline; no feed started.')
# Preserve the supported ojd argv[0] symlink, do NOT resolve it to the GUI name.
command=[str(a.cli.absolute()),'--no-input','--json','--timeout','5','virtual','feed','--as',a.profile]
p=subprocess.Popen(command,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
during=[]; forced=False
try:
    p.stdin.write('{}\n');p.stdin.flush()
    for unused in range(6):
        time.sleep(.5);during=snapshot()
        if during or p.poll() is not None:break
finally:
    p.stdin.close();p.stdin=None
    try:stdout,stderr=p.communicate(timeout=8)
    except subprocess.TimeoutExpired:
        forced=True;p.terminate()
        try:stdout,stderr=p.communicate(timeout=5)
        except subprocess.TimeoutExpired:p.kill();stdout,stderr=p.communicate()
for unused in range(8):
    after=snapshot()
    if not after:break
    time.sleep(.25)
record={'observedAtUTC':datetime.datetime.now(datetime.timezone.utc).isoformat(),'profile':a.profile,
        'neutralOnly':True,'before':before,'during':during,'after':after,'exitCode':p.returncode,
        'forcedOwnChildTermination':forced,'stdout':stdout.replace(str(pathlib.Path.home()),'~'),
        'stderr':stderr.replace(str(pathlib.Path.home()),'~'),
        'creationObserved':bool(during),'absenceAfterTrialConfirmed':not after,
        'removalObserved':bool(during) and not after,'sonyOperated':False,'keyboardCaptured':False}
(a.output/'RESULT.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(record,ensure_ascii=False,indent=2))
raise SystemExit(0 if p.returncode == 0 and during and not after and not forced else 1)
