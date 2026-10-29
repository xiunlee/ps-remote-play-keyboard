// Standalone research fixture. This is not the upstream Windows server CLI.
// It exercises pinned userspace USB/IP + V5 code without a Windows USB import.
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
	labMarker = "pskeyboard-dualsense-v5-userspace-only"
	deviceType = "dualsensecombinedaudioduplexv5"
	apiAddress = "127.0.0.1:33442"
	usbAddress = "127.0.0.1:33441"
	lifetime = 180 * time.Second
)

func ping(_ *api.Request, res *api.Response, _ *slog.Logger) error {
	data, err := json.Marshal(map[string]any{
		"server": "PSKeyboard-DualSenseV5-Lab", "version": "research.1",
		"upstreamVersion": "v0.1.9-rc4.6.6", "sourceCommit": sourceCommit,
		"labMarker": labMarker, "autoAttach": false,
		"apiAddress": apiAddress, "usbAddress": usbAddress,
		"windowsAttached": false, "sonyTested": false,
	})
	res.JSON = string(data)
	return err
}

// The fixture exposes only the selected 33-byte composite persona, without
// metadata/identity overrides or a path that enables auto-attach at runtime.
func onlyCandidate(next api.HandlerFunc) api.HandlerFunc {
	return func(req *api.Request, res *api.Response, logger *slog.Logger) error {
		var request struct { Type string `json:"type"` }
		decoder := json.NewDecoder(strings.NewReader(req.Payload))
		decoder.DisallowUnknownFields()
		if err := decoder.Decode(&request); err != nil {
			return apierror.ErrBadRequest("lab accepts only the exact candidate type")
		}
		var trailing any
		if err := decoder.Decode(&trailing); !errors.Is(err, io.EOF) || request.Type != deviceType {
			return apierror.ErrBadRequest("lab accepts only the exact candidate type")
		}
		return next(req, res, logger)
	}
}

func run() error {
	// No environment/config-file loading, tray, keyfile, installer, driver
	// probes, native attach, raw packet logging, or keyboard hook is invoked.
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	slog.SetDefault(logger)
	usbServer := usb.New(usb.ServerConfig{
		Addr: usbAddress, ConnectionTimeout: 3*time.Second,
		BusCleanupTimeout: 5*time.Second,
	}, logger, viiperlog.NewRaw(nil))
	apiServer := api.New(usbServer, apiAddress, api.ServerConfig{
		Addr: apiAddress, AutoAttachLocalClient: false,
		RequireLocalHostAuth: false, ConnectionTimeout: 3*time.Second,
		DeviceHandlerConnectTimeout: 5*time.Second,
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
		select { case stop <- struct{}{}: default: }
		return nil
	})

	usbDone := make(chan error, 1)
	go func() { usbDone <- usbServer.ListenAndServe() }()
	defer apiServer.Close()
	defer usbServer.Close()
	select {
	case err := <-usbDone:
		return fmt.Errorf("USB listener failed: %w", err)
	case <-usbServer.Ready():
	case <-time.After(5*time.Second):
		return errors.New("USB listener readiness timeout")
	}
	if err := apiServer.Start(); err != nil { return err }
	fmt.Printf("READY labMarker=%s sourceCommit=%s api=%s usb=%s autoAttach=false lifetime_seconds=180\n",
		labMarker, sourceCommit, apiAddress, usbAddress)
	go func() {
		scanner := bufio.NewScanner(os.Stdin)
		for scanner.Scan() {
			if scanner.Text() == "stop" {
				select { case stop <- struct{}{}: default: }
				return
			}
		}
	}()
	select {
	case err := <-usbDone: return fmt.Errorf("USB listener ended: %w", err)
	case <-time.After(lifetime):
	case <-stop:
	}
	apiServer.Close()
	if err := usbServer.Close(); err != nil { return err }
	fmt.Println("STOPPED windows_attached=false sony_tested=false")
	return nil
}

func main() {
	if len(os.Args) != 2 || os.Args[1] != "--isolated-userspace-only" {
		fmt.Fprintln(os.Stderr, "Usage: DualSenseV5LabServer.exe --isolated-userspace-only")
		os.Exit(2)
	}
	// An independent process-lifetime ceiling also bounds unforeseen shutdown
	// stalls in the research dependencies. The OS closes this process's sockets.
	timer := time.AfterFunc(lifetime + 15*time.Second, func() { os.Exit(3) })
	defer timer.Stop()
	if err := run(); err != nil {
		fmt.Fprintln(os.Stderr, "FAIL", err)
		os.Exit(1)
	}
}
