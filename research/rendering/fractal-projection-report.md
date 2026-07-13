# Cube-Sphere Projection Report

Unit sphere area target per cube-coordinate area is `pi / 6`.
Lower mean absolute relative area error is better for terrain density.

| Projection | Face | Samples | Avg area scale | Min rel area | Max rel area | Mean abs rel error | RMS rel error |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| normalize | PositiveX | 16 | 0.524151 | 0.417233 | 1.887723 | 0.315999 | 0.378463 |
| normalize | NegativeX | 16 | 0.524151 | 0.417233 | 1.887723 | 0.315999 | 0.378463 |
| normalize | PositiveY | 16 | 0.524151 | 0.417233 | 1.887723 | 0.315999 | 0.378463 |
| normalize | NegativeY | 16 | 0.524151 | 0.417233 | 1.887723 | 0.315999 | 0.378463 |
| normalize | PositiveZ | 16 | 0.524151 | 0.417233 | 1.887723 | 0.315999 | 0.378463 |
| normalize | NegativeZ | 16 | 0.524151 | 0.417233 | 1.887723 | 0.315999 | 0.378463 |
| tangent | PositiveX | 16 | 0.523872 | 0.873549 | 1.175269 | 0.075933 | 0.088550 |
| tangent | NegativeX | 16 | 0.523872 | 0.873549 | 1.175269 | 0.075933 | 0.088550 |
| tangent | PositiveY | 16 | 0.523872 | 0.873549 | 1.175269 | 0.075933 | 0.088550 |
| tangent | NegativeY | 16 | 0.523872 | 0.873549 | 1.175269 | 0.075933 | 0.088550 |
| tangent | PositiveZ | 16 | 0.523866 | 0.873549 | 1.175245 | 0.075944 | 0.088557 |
| tangent | NegativeZ | 16 | 0.523866 | 0.873549 | 1.175245 | 0.075944 | 0.088557 |

Default for the first terrain slice: `tangent`.
It is still cheap, remains seam-compatible with the cube face mapping, and the sampler gives it lower area spread than direct normalization on this harness.
