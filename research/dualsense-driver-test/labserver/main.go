// Standalone research fixture. This is not the upstream Windows server CLI.
// Hosted-CI-only fixture. Driver installation/import belongs to the external test runner.
package main

import (
	"bufio"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"os"
	"strings"
	"time"

	_ "github.com/Alia5/VIIPER/device/dualsense"
	viiperlog "github.com/Alia5/VIIPER/internal/log"
	"github.com/Alia5/VIIPER/internal/server/api"
	apierror "github.com/Alia5/VIIPER/internal/server/api/error"
	"github.com/Alia5/VIIPER/internal/server/api/handler"
	"github.com/Alia5/VIIPER/internal/server/usb"
)

const (
	sourceCommit = "b78e31e93e84b4cb7c4b4c15b8aace10749106d2"
	labMarker    = "pskeyboard-dualsense-v5-driver-test"
	apiAddress   = "127.0.0.1:33542"
	usbAddress   = "127.0.0.1:33541"
	lifetime     = 300 * time.Second
)

func ping(_ *api.Request, res *api.Response, _ *slog.Logger) error {
	data, err := json.Marshal(map[string]any{
		"server": "PSKeyboard-DualSenseV5-DriverLab", "version": "research.driver.1",
		"upstreamVersion": "v0.1.9-rc4.6.6", "sourceCommit": sourceCommit,
		"labMarker": labMarker, "autoAttach": false,
		"apiAddress": apiAddress, "usbAddress": usbAddress,
		"windowsAttachManagedExternally": true, "hostedRunnerOnly": true, "sonyTested": false,
	})
	res.JSON = string(data)
	return err
}

// The fixture exposes only the four selected 33-byte V5 personas, without
// metadata/identity overrides or a path that enables auto-attach at runtime.
func onlyCandidate(next api.HandlerFunc) api.HandlerFunc {
	return func(req *api.Request, res *api.Response, logger *slog.Logger) error {
		var request struct {
			Type string `json:"type"`
		}
		decoder := json.NewDecoder(strings.NewReader(req.Payload))
		decoder.DisallowUnknownFields()
		if err := decoder.Decode(&request); err != nil {
			return apierror.ErrBadRequest("lab accepts only the exact candidate type")
		}
		var trailing any
		if err := decoder.Decode(&trailing); !errors.Is(err, io.EOF) {
			return apierror.ErrBadRequest("lab accepts only the exact candidate type")
		}
		switch request.Type {
		case "dualsensecombinedaudioduplexv5", "dualsensegamepadv5",
			"dualsenseedgecombinedaudioduplexv5", "dualsenseedgegamepadv5":
		default:
			return apierror.ErrBadRequest("lab accepts only a selected V5 candidate type")
		}
		return next(req, res, logger)
	}
}

func run() error {
	// Check again at the actual server-start boundary. Environment flags guard
	// against accidental local execution; they are not cryptographic attestation.
	if err := validateHostedEnvironment(os.Getenv); err != nil {
		return err
	}
	// No VIIPER environment/config-file loading, tray, keyfile, installer, driver
	// probes, native attach, raw packet logging, or keyboard hook is invoked.
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	slog.SetDefault(logger)
	usbServer := usb.New(usb.ServerConfig{
		Addr: usbAddress, ConnectionTimeout: 3 * time.Second,
		BusCleanupTimeout: 5 * time.Second,
	}, logger, viiperlog.NewRaw(nil))
	apiServer := api.New(usbServer, apiAddress, api.ServerConfig{
		Addr: apiAddress, AutoAttachLocalClient: false,
		RequireLocalHostAuth: false, ConnectionTimeout: 3 * time.Second,
		DeviceHandlerConnectTimeout: 5 * time.Second,
	}, logger)
	apiServer.Config().AutoAttachWindowsNative = false
	stop := make(chan struct{}, 1)
	router := apiServer.Router()
	router.Register("ping", ping)
	router.Register("bus/list", handler.BusList(usbServer))
	router.Register("bus/create", handler.BusCreate(usbServer))
	router.Register("bus/remove", handler.BusRemove(usbServer))
	router.Register("bus/{id}/list", handler.BusDevicesList(usbServer))
	router.Register("bus/{id}/add", onlyCandidate(handler.BusDeviceAdd(usbServer, apiServer)))
	router.RegisterStream("bus/{busId}/{deviceid}", api.DeviceStreamHandler(usbServer))
	router.Register("lab/stop", func(_ *api.Request, res *api.Response, _ *slog.Logger) error {
		res.JSON = `{"stopping":true}`
		select {
		case stop <- struct{}{}:
		default:
		}
		return nil
	})

	usbDone := make(chan error, 1)
	go func() { usbDone <- usbServer.ListenAndServe() }()
	defer func() { apiServer.Close(); _ = usbServer.Close() }()
	select {
	case err := <-usbDone:
		return fmt.Errorf("USB listener failed: %w", err)
	case <-usbServer.Ready():
	case <-time.After(5 * time.Second):
		return errors.New("USB listener readiness timeout")
	}
	if err := apiServer.Start(); err != nil {
		return err
	}
	fmt.Printf("READY labMarker=%s sourceCommit=%s api=%s usb=%s autoAttach=false lifetime_seconds=300\n",
		labMarker, sourceCommit, apiAddress, usbAddress)
	go func() {
		scanner := bufio.NewScanner(os.Stdin)
		for scanner.Scan() {
			if scanner.Text() == "stop" {
				select {
				case stop <- struct{}{}:
				default:
				}
				return
			}
		}
	}()
	select {
	case err := <-usbDone:
		return fmt.Errorf("USB listener ended: %w", err)
	case <-time.After(lifetime):
	case <-stop:
	}
	apiServer.Close()
	if err := usbServer.Close(); err != nil {
		return err
	}
	fmt.Println("STOPPED driver_cleanup_managed_externally=true sony_tested=false")
	return nil
}

func validateHostedEnvironment(getenv func(string) string) error {
	for name, expected := range map[string]string{
		"GITHUB_ACTIONS":     "true",
		"RUNNER_ENVIRONMENT": "github-hosted",
		"GITHUB_REPOSITORY":  "xiunlee/ps-remote-play-keyboard",
		"RUNNER_OS":          "Windows",
	} {
		if getenv(name) != expected {
			return fmt.Errorf("hosted driver fixture refused: %s does not match the required environment", name)
		}
	}
	return nil
}

func main() {
	if len(os.Args) != 2 || os.Args[1] != "--isolated-driver-test-only" {
		fmt.Fprintln(os.Stderr, "Usage: DualSenseV5DriverLabServer.exe --isolated-driver-test-only")
		os.Exit(2)
	}
	if err := validateHostedEnvironment(os.Getenv); err != nil {
		fmt.Fprintln(os.Stderr, "FAIL", err)
		os.Exit(2)
	}
	// An independent process-lifetime ceiling also bounds unforeseen shutdown
	// stalls in the research dependencies. The OS closes this process's sockets.
	timer := time.AfterFunc(lifetime+15*time.Second, func() { os.Exit(3) })
	defer timer.Stop()
	if err := run(); err != nil {
		fmt.Fprintln(os.Stderr, "FAIL", err)
		os.Exit(1)
	}
}
