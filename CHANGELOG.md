# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/).

## [1.1.0] - 2026-07-16

### Added

- EditMode and PlayMode tests covering OSC serialization round-trips and loopback delivery.
- LICENSE file (MIT).
- Static receivers and clients are reset on domain reload.

### Changed

- OSCServer now shuts down cooperatively instead of using Thread.Abort, and its receive thread is a background thread that exits cleanly on Close.
- Incoming message logging moved off the receive thread to the main thread.
- OSCBundle.Pack now throws NotSupportedException stating that sending bundles is not supported.
- The receive loop idles for at least 1 ms instead of spinning when no data is available.

### Removed

- Legacy OSCHandler, superseded by OSCMaster.
- Unused OSCClient.LogOutgoing and OSCPacket.server fields.

### Fixed

- OSC packet parsing is bounds-checked, so a truncated packet no longer reads past the buffer and kills the receive thread.
- OSCClient.SendTo and OSCMaster.SendMessage no longer leak a UdpClient on every call.
- OSCServer.Close no longer throws when the socket was never opened or is closed twice.
- The receive loop no longer throws ObjectDisposedException when leaving play mode.
- OSCMaster.RemoveClient, RemoveReceiver and SendMessageUsingClient no longer throw on an unknown id.
- OSCClient.Connect preserves the original exception.


## [1.0.1] - 2026-04-22

### Changed

- OSCMaster now add itself to DontDestroyOnLoad.

### Fixed

- Fixed OSCMaster Clients and Receivers being reset on Awake.
- Fixed multiple OSCMaster possibility. Singleton pattern is now correctly enforced.


## [1.0.0] - 2026-04-21

### Added

- Set up repository and files for UPM support.
