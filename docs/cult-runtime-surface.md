# Cult Runtime Surface

Fensalir exposes the runtime cache path and schema catalog boundary; client
repos own their typed CultCache documents.

## Cache Path

`AquariumHost` parses `--cache <path>` and passes the result through
`AquariumRuntimeOptions.CultCachePath`. Direct runs can also use
`AQUARIUM_CULTCACHE_PATH`.

The dev scripts pass a repo-local cache path under:

```powershell
E:\Projects\Fensalir\artifacts\dev-reload\cultcache\aquarium-client.msgpack
```

Headless runs use a separate headless cache directory so smoke tests do not
overwrite the visible dev window's state.

## Ownership

Fensalir does not define every client document. A client runtime decides which
typed CultCache documents it persists: camera state, graphics settings, body
state, UI state, transport state, or anything else the client owns.

Shared settings that the host/renderer must understand belong in
`Aquarium.Engine.Contracts`. Client-specific schemas stay in the client repo.

## Failure Boundary

A truncated or unreadable single-file CultCache backing store is not a schema
migration failure. Client runtimes should quarantine corrupt snapshots and boot
fresh typed state instead of treating broken bytes as missing defaults.

Live reload should flush the current runtime before loading the next client
assembly so changed settings survive assembly reload.
