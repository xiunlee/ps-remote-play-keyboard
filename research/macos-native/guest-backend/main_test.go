package main

import (
	"bytes"
	"encoding/binary"
	"errors"
	"hash/crc32"
	"strings"
	"testing"
	"time"

	"github.com/Alia5/VIIPER/device/dualsense"
)

// Pure argument/JSON/reader checks only. No tests call run/main, bind sockets,
// create a USB device, invoke a driver, or access Sony Remote Play.
func TestCandidateArguments(t *testing.T) {
	for _, args := range [][]string{nil, {}, {"--guest-candidate-preview", "extra"}, {"--isolated-driver-test-only"}, {"--CANDIDATE-PREVIEW"}} {
		if validArguments(args) {
			t.Fatalf("accepted non-exact arguments: %v", args)
		}
	}
	if !validArguments([]string{"--guest-candidate-preview"}) {
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

func TestGuestRestrictions(t *testing.T) {
	usbConfig, apiConfig := restrictedConfigs()
	if usbConfig.Addr != "127.0.0.1:33641" || apiConfig.Addr != "127.0.0.1:33642" {
		t.Fatal("guest listeners must remain fixed loopback")
	}
	if apiConfig.AutoAttachLocalClient || apiConfig.AutoAttachWindowsNative {
		t.Fatal("host/guest attach must be explicitly managed externally")
	}
	if sourceCommit != "b78e31e93e84b4cb7c4b4c15b8aace10749106d2" || shutdownTimeout != 15*time.Second {
		t.Fatal("source pin or bounded shutdown changed")
	}
	if validArguments([]string{"--candidate-preview"}) {
		t.Fatal("guest fork must not masquerade as the Windows candidate")
	}
}

func TestGuestNeutralWire(t *testing.T) {
	// Pure upstream codec oracle: this constructs state values, not a device.
	want, err := dualsense.NewInputState().MarshalBinary()
	if err != nil { t.Fatal(err) }
	state := make([]byte, 33)
	state[15], state[20], state[32] = 0x80, 0x80, 0xe0
	if !bytes.Equal(state, want) { t.Fatalf("neutral state differs from pinned codec: %x != %x", state, want) }
	packet := make([]byte, 49)
	copy(packet, "VPCM")
	packet[4], packet[5], packet[6] = 5, 1, 33
	copy(packet[16:], state)
	checksum := crc32.NewIEEE()
	_, _ = checksum.Write(packet[4:12])
	_, _ = checksum.Write(state)
	binary.LittleEndian.PutUint32(packet[12:16], checksum.Sum32())
	if binary.LittleEndian.Uint32(packet[12:16]) != 0xB88DB821 {
		t.Fatalf("neutral CRC changed: %08x", checksum.Sum32())
	}
}
