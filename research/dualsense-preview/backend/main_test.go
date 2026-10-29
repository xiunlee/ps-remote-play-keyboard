package main

import (
	"errors"
	"strings"
	"testing"
	"time"
)

// Pure argument/JSON/reader checks only. No tests call run/main, bind sockets,
// create a USB device, invoke a driver, or access Sony Remote Play.
func TestCandidateArguments(t *testing.T) {
	for _, args := range [][]string{nil, {}, {"--candidate-preview", "extra"}, {"--isolated-driver-test-only"}, {"--CANDIDATE-PREVIEW"}} {
		if validArguments(args) {
			t.Fatalf("accepted non-exact arguments: %v", args)
		}
	}
	if !validArguments([]string{"--candidate-preview"}) {
		t.Fatal("rejected the sole candidate-preview argument")
	}
}

func TestCandidateAllowlist(t *testing.T) {
	if err := validateCandidate(`{"type":"dualsensegamepadv5"}`); err != nil {
		t.Fatalf("selected gamepad persona rejected: %v", err)
	}
	for _, payload := range []string{
		`{}`, `null`, `[]`, `{"type":"dualsense"}`, `{"type":"dualsensegamepad"}`,
		`{"type":"dualsensecombinedaudioduplexv5"}`, `{"type":"dualsenseedgecombinedaudioduplexv5"}`,
		`{"type":"dualsenseedgegamepadv5"}`, `{"type":"dualsenseaudioonlyv5"}`,
		`{"type":"DualSenseGamepadV5"}`, `{"type":"dualsensegamepadv5 "}`,
		`{"type":"dualsensegamepadv5","autoAttach":true}`,
		`{"type":"dualsensegamepadv5","serial":"override"}`,
		`{"type":"dualsensegamepadv5"}{}`, `{"type":"dualsensegamepadv5"}junk`,
	} {
		if err := validateCandidate(payload); err == nil {
			t.Fatalf("unsafe/noncanonical device request accepted: %s", payload)
		}
	}
}

func TestCandidateShutdownWatchdog(t *testing.T) {
	t.Run("completed cleanup cancels watchdog and preserves errors", func(t *testing.T) {
		var order []string
		forced := make(chan int, 1)
		closeError := errors.New("USB close failed")
		err := shutdownServers(func() { order = append(order, "api") }, func() error {
			order = append(order, "usb")
			return closeError
		}, 20*time.Millisecond, func(code int) { forced <- code })
		if err != closeError || strings.Join(order, ",") != "api,usb" {
			t.Fatalf("cleanup lost its order or error: %v %v", order, err)
		}
		select {
		case code := <-forced:
			t.Fatalf("completed cleanup left an active watchdog: %d", code)
		case <-time.After(50 * time.Millisecond):
		}
	})
	t.Run("a blocked close triggers only the shutdown watchdog", func(t *testing.T) {
		entered := make(chan struct{})
		release := make(chan struct{})
		forced := make(chan int, 1)
		done := make(chan error, 1)
		go func() {
			done <- shutdownServers(func() { close(entered); <-release }, func() error { return nil },
				20*time.Millisecond, func(code int) { forced <- code })
		}()
		<-entered
		select {
		case code := <-forced:
			if code != 3 {
				t.Errorf("unexpected forced shutdown code: %d", code)
			}
		case <-time.After(time.Second):
			t.Error("blocked shutdown did not trigger its watchdog")
		}
		close(release)
		if err := <-done; err != nil {
			t.Fatal(err)
		}
	})
}

type failedReader struct{}

func (failedReader) Read([]byte) (int, error) { return 0, errors.New("closed pipe") }

func TestCandidateStdinStops(t *testing.T) {
	for _, sample := range []struct{ input, want string }{
		{"", "stdin_eof"}, {"ignored\n", "stdin_eof"}, {"stop\nignored\n", "stdin_stop"},
		{"stop\r\n", "stdin_stop"}, {"stop", "stdin_stop"}, {"STOP\n", "stdin_eof"},
		{"ignored\nstop\n", "stdin_stop"},
	} {
		if got := stdinStopReason(strings.NewReader(sample.input)); got != sample.want {
			t.Fatalf("stdin %q: got %s, want %s", sample.input, got, sample.want)
		}
	}
	if got := stdinStopReason(failedReader{}); got != "stdin_error" {
		t.Fatalf("read failure did not stop the session: %s", got)
	}
}
