// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation
import Darwin

@main
struct PSKeyboardRuntimeMain {
    static func main() {
        signal(SIGPIPE,SIG_IGN)
        guard CommandLine.arguments.count == 4 else {
            RuntimeOwner.status("error","runtime_arguments"); exit(64)
        }
        let stop = RuntimeStopFlag()
        // The App owns stdin. Normal stop, App termination and App crashes all
        // close that lease. No global process-name termination is used.
        Thread {
            var buffer = [UInt8](repeating:0,count:64)
            while true {
                let count = read(STDIN_FILENO,&buffer,buffer.count)
                if count < 0 && errno == EINTR { continue }
                if count <= 0 || buffer.prefix(max(0,count)).contains(10) { stop.request(); return }
            }
        }.start()
        signal(SIGTERM,SIG_IGN); signal(SIGINT,SIG_IGN)
        let terminate = DispatchSource.makeSignalSource(signal:SIGTERM,queue:.global())
        let interrupt = DispatchSource.makeSignalSource(signal:SIGINT,queue:.global())
        terminate.setEventHandler { stop.request() }; interrupt.setEventHandler { stop.request() }
        terminate.resume(); interrupt.resume()
        let runtime = RuntimeOwner(resources:URL(fileURLWithPath:CommandLine.arguments[1]),
            helpers:URL(fileURLWithPath:CommandLine.arguments[2]),support:URL(fileURLWithPath:CommandLine.arguments[3]),stop:stop)
        do { try runtime.run(); exit(0) }
        catch let error as RuntimeFailure { RuntimeOwner.status("error",error.code); exit(1) }
        catch { RuntimeOwner.status("error","runtime_failure"); exit(1) }
    }
}
