// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Guest-only fork of the restricted candidate entry point. No host attach.
// Both listeners remain fixed to guest loopback; the VM owner supplies vsock relays.
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
	sourceCommit    = "b78e31e93e84b4cb7c4b4c15b8aace10749106d2"
	labMarker       = "pskeyboard-dualsense-v5-guest"
	apiAddress      = "127.0.0.1:33642"
	usbAddress      = "127.0.0.1:33641"
	shutdownTimeout = 15 * time.Second
)

func ping(_ *api.Request, res *api.Response, _ *slog.Logger) error {
	data, err := json.Marshal(map[string]any{
		"server": "PSKeyboard-DualSenseV5-GuestCandidate", "version": "research.guest.1",
		"upstreamVersion": "v0.1.9-rc4.6.6", "sourceCommit": sourceCommit,
		"labMarker": labMarker, "autoAttach": false,
		"apiAddress": apiAddress, "usbAddress": usbAddress,
		"guestAttachManagedExternally": true, "candidatePreview": true,
        "loopbackOnly": true, "hostAutoAttach": false,
		"sessionLimitSeconds": 0, "sessionLifetimeLimit": false,
		"shutdownTimeoutSeconds": 15, "stdinEofStops": true, "sonyTested": false,
	})
	res.JSON = string(data)
	return err
}

func validateCandidate(payload string) error {
	var request struct {
		Type string `json:"type"`
	}
	decoder := json.NewDecoder(strings.NewReader(payload))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(&request); err != nil {
		return apierror.ErrBadRequest("candidate preview accepts only an exact selected type")
	}
	var trailing any
	if err := decoder.Decode(&trailing); !errors.Is(err, io.EOF) {
		return apierror.ErrBadRequest("candidate preview accepts one device request")
	}
	switch request.Type {
	case "dualsensegamepadv5":
		return nil
	default:
		return apierror.ErrBadRequest("candidate preview accepts only a selected V5 type")
	}
}

func onlyCandidate(next api.HandlerFunc) api.HandlerFunc {
	return func(req *api.Request, res *api.Response, logger *slog.Logger) error {
		if err := validateCandidate(req.Payload); err != nil {
			return err
		}
		return next(req, res, logger)
	}
}

// The guest supervisor keeps the redirected stdin pipe open while the session is owned.
// EOF, a read error, or an exact stop line all end the backend session. No
// parent-process query or global process-name termination is required.
func stdinStopReason(reader io.Reader) string {
	scanner := bufio.NewScanner(reader)
	for scanner.Scan() {
		if scanner.Text() == "stop" {
			return "stdin_stop"
		}
	}
	if scanner.Err() != nil {
		return "stdin_error"
	}
	return "stdin_eof"
}

// The watchdog exists only while shutdown is in progress. A healthy active
// session has no timer that can disconnect it. Function arguments allow pure
// tests of cleanup ordering and a stuck close without opening any sockets.
func shutdownServers(closeAPI func(), closeUSB func() error, timeout time.Duration, forceExit func(int)) error {
	watchdog := time.AfterFunc(timeout, func() { forceExit(3) })
	defer watchdog.Stop()
	closeAPI()
	return closeUSB()
}

func restrictedConfigs() (usb.ServerConfig, api.ServerConfig) {
	usbConfig := usb.ServerConfig{
		Addr: usbAddress, ConnectionTimeout: 3 * time.Second,
		BusCleanupTimeout: 5 * time.Second,
	}
	apiConfig := api.ServerConfig{
		Addr: apiAddress, AutoAttachLocalClient: false,
		RequireLocalHostAuth: false, ConnectionTimeout: 3 * time.Second,
		DeviceHandlerConnectTimeout: 5 * time.Second,
	}
	apiConfig.AutoAttachWindowsNative = false
	return usbConfig, apiConfig
}

func run() (runErr error) {
	stop := make(chan string, 1)
	go func() { stop <- stdinStopReason(os.Stdin) }()
	// No environment/config loading, keyfile, tray, installer, driver probes,
	// native attach, raw packet logging, keyboard hooks or Sony process access.
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	slog.SetDefault(logger)
	usbConfig, apiConfig := restrictedConfigs()
	usbServer := usb.New(usbConfig, logger, viiperlog.NewRaw(nil))
	apiServer := api.New(usbServer, apiAddress, apiConfig, logger)
	router := apiServer.Router()
	router.Register("ping", ping)
	router.Register("bus/list", handler.BusList(usbServer))
	router.Register("bus/create", handler.BusCreate(usbServer))
	router.Register("bus/remove", handler.BusRemove(usbServer))
	router.Register("bus/{id}/list", handler.BusDevicesList(usbServer))
	router.Register("bus/{id}/add", onlyCandidate(handler.BusDeviceAdd(usbServer, apiServer)))
	router.RegisterStream("bus/{busId}/{deviceid}", api.DeviceStreamHandler(usbServer))

	reason := "stdin_stop"
	defer func() {
		closeErr := shutdownServers(apiServer.Close, usbServer.Close, shutdownTimeout, os.Exit)
		if runErr == nil {
			runErr = closeErr
		}
		if runErr == nil {
			fmt.Printf("STOPPED reason=%s driver_cleanup_managed_externally=true sony_tested=false\n", reason)
		}
	}()
	usbDone := make(chan error, 1)
	go func() { usbDone <- usbServer.ListenAndServe() }()
	select {
	case err := <-usbDone:
		return fmt.Errorf("USB listener failed: %w", err)
	case <-usbServer.Ready():
	case reason = <-stop:
		return nil
	case <-time.After(5 * time.Second):
		return errors.New("USB listener readiness timeout")
	}
	if err := apiServer.Start(); err != nil {
		return err
	}
	fmt.Printf("READY labMarker=%s sourceCommit=%s api=%s usb=%s autoAttach=false session_lifetime_limit=false\n",
		labMarker, sourceCommit, apiAddress, usbAddress)
	select {
	case err := <-usbDone:
		return fmt.Errorf("USB listener ended: %w", err)
	case reason = <-stop:
	}
	return nil
}

func validArguments(args []string) bool {
	return len(args) == 1 && args[0] == "--guest-candidate-preview"
}

func main() {
	if !validArguments(os.Args[1:]) {
		fmt.Fprintln(os.Stderr, "Usage: GuestDualSenseBackend --guest-candidate-preview")
		os.Exit(2)
	}
	if err := run(); err != nil {
		fmt.Fprintln(os.Stderr, "FAIL", err)
		os.Exit(1)
	}
}
