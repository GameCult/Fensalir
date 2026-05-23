# Engine And Client Boundary

Fensalir is the reusable native runtime. Client repos own meaning.

`Aquarium.Engine` owns the native machinery: Win32 windowing, D3D12 device
state, swapchain presentation, resource lifetime, shader compilation, reload
transport, input translation, render target allocation, audio output, debug
chrome, and render graph execution.

`Aquarium.Engine.Contracts` owns the Vortice-free public boundary clients use to
describe runtime state, render intent, UI, audio, input, frame data, and
persistent settings. Contracts may name reusable engine concepts. They may not
smuggle one client repo's policy into the host under a nicer namespace.

External clients, including Epiphany Aquarium at `E:\Projects\Aquarium-Engine`,
own their app semantics, state documents, client shaders, client render plans,
and narrative layout rules.

## Hard Rules

- Engine code may expose rendering abstractions. It may not know client role
  names, app-specific state semantics, or frontend-specific layout policy.
- Clients may configure cameras, render targets, shader passes, body registries,
  UI panels, audio documents, and resource bindings through engine APIs. They may
  not reach into D3D12 implementation details.
- Hot reload is transport. It is not architecture. The reload boundary exists so
  clients can change without killing the device, not so policy can leak into the
  engine under a nicer hat.
- If a feature owns descriptors, command lists, fences, barriers, render target
  transitions, shader object lifetime, or swapchain presentation, its default
  home is `Aquarium.Engine`.
- If a feature owns app identity, role semantics, story state, user-facing
  layout meaning, or transport-specific interpretation, its default home is the
  client repo unless it is reduced to a reusable data-only contract.

## Engine API Shape

Durable renderer APIs should let clients configure a small set of engine
primitives:

- Render targets: format, dimensions, history policy, clear policy, and exported
  handles.
- Cameras: view/projection data, previous-frame data, jitter policy, and named
  consumers.
- Shader passes: shader module, root bindings, target outputs, depth policy,
  dispatch/draw shape, and reload identity.
- Scene resources: structured buffers, textures, samplers, proxy registries, and
  light registries behind typed handles.
- Frame graph edges: explicit read/write dependencies so pass ordering is data,
  not hidden renderer folklore.

Do not build this by piling adapters around the current monolith. Expose the
smallest real engine abstraction that lets one client-owned concept move through
without D3D12 details, then repeat.
