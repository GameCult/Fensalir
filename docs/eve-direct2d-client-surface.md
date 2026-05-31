# Eve Direct2D Client Surface

Fensalir's DirectWrite/Direct2D overlay is an Eve client surface.

That means it should consume the same CultMesh UI surface documents as the
browser, iOS, Android, and Flutter clients. It should not become a second
dashboard language or an app-specific policy owner. The provider still owns
state and side effects; Fensalir owns the native desktop/runtime lowering.

## Authority Map

- Owner: provider repos own surface state, accepted commands, and meaning.
- Input document: the shared Eve/CultMesh surface contract owns panels, nodes,
  assets, commands, selection, and state paths.
- Lowering: Fensalir translates the surface document into `AquariumUiDocument`
  panels and controls.
- Renderer: `DirectWriteOverlay` draws the resulting panels through the
  D3D11-on-12 Direct2D bridge after the D3D12 scene.
- Output: Fensalir emits user commands back through the same Eve/CultNet command
  path as browser and mobile clients.

## Current Landing Zone

The existing engine contract already has the right narrow surface:

- `AquariumUiDocument`
- `AquariumUiPanel`
- `AquariumUiControl`
- `D3D12Renderer.UpdateUi`
- `DirectWriteOverlay`

The first implementation should add an adapter from the shared Eve surface
document into `AquariumUiDocument`. Do not bypass that path by teaching the
renderer about VoidBot, Mimir, or any provider-specific dashboard. Provider
specificity belongs in the provider document.

## Invariants

- Browser Eve remains the behavior and layout reference.
- Direct2D is a renderer for the shared contract, not the contract owner.
- Fensalir may own native text metrics, focus, keyboard, pointer dispatch,
  opacity, panel positioning, and GPU/CPU timing telemetry.
- Fensalir must not own app-specific command semantics.
- Images and media assets need explicit asset handles/cache keys before they are
  renderer inputs.
- High-frequency sensor data stays in the timestamped sensor path, not in UI
  panel state.

## First Cut

1. Define the shared Eve surface DTO in a neutral contract package or mirror the
   first schema in Fensalir as an adapter boundary.
2. Map simple nodes into `AquariumUiPanel` and existing controls:
   readouts, buttons, toggles, sliders, options, text boxes, tree items, and
   detail panes.
3. Route control activation back to a provider command sink.
4. Add a replay smoke using a recorded VoidBot/Mimir surface fixture.
5. Compare the Direct2D rendering against the browser reference fixture before
   adding provider-specific polish.
