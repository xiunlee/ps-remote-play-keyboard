package main

import (
	"errors"
	"strings"
	"testing"
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
	for _, persona := range []string{"dualsensecombinedaudioduplexv5", "dualsensegamepadv5", "dualsenseedgecombinedaudioduplexv5", "dualsenseedgegamepadv5"} {
		if err := validateCandidate(`{"type":"` + persona + `"}`); err != nil {
			t.Fatalf("selected persona rejected: %s: %v", persona, err)
		}
	}
	for _, payload := range []string{
		`{}`, `null`, `[]`, `{"type":"dualsense"}`, `{"type":"dualsensegamepad"}`,
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
