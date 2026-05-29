# Planetary Flow Organ Research Notes

This note distills the research sidecar for a Fensalir organ that predicts
stochastic advection parameter sets over cube-sphere planetary domains.

## 1. Stochastic Lagrangian And Plastic Transport

PlasticAdrift/Adrift is best treated as a stochastic transition target, not as a
velocity field. The Adrift paper describes a statistical transition-matrix
representation built from trajectories of more than 15,000 surface drifters,
and the PlasticAdrift site refers to its own transition matrix and ten-year
probability animations.

Implementation implication: use PlasticAdrift as auxiliary supervision or
validation for surface transport kernels:

```text
cell now -> probability over future cells
```

This can train or check neighbor-kernel logits, effective diffusivity, seasonal
connectivity, and multi-step debris spread. It should not be the only ocean
truth because it represents observed surface drift statistics, not full ocean
state.

Sources:

- PlasticAdrift: https://plasticadrift.org/
- Adrift.org.au paper abstract: https://www.sciencedirect.com/science/article/abs/pii/S0022098114002445
- McAdam and van Sebille transition matrices: https://dbc.library.uu.nl/handle/1874/362629

## 2. Bathymetry And Topography

GEBCO_2025 is the best current high-resolution global relief/bathymetry source
for broad use. It provides global land/ocean elevation on a 15 arc-second grid,
with 43,200 x 86,400 samples, NetCDF distribution, WGS84 assumption, and a
source-type identifier grid. GEBCO asks for attribution in publications.

NOAA ETOPO 2022 is a strong baseline because it is a 15 arc-second global
relief model and the ESSD dataset paper states that ETOPO data are covered by
CC0-1.0 in NOAA metadata. It is especially useful where a permissive licensing
posture matters more than the newest GEBCO revision.

Implementation implication: ingest both. Use ETOPO as the permissive default
and GEBCO as the higher-confidence bathymetry option with explicit attribution
and source-type channels.

Sources:

- GEBCO_2025 Grid: https://www.gebco.net/data-products-gridded-bathymetry-data/gebco2025-grid
- GEBCO gridded data: https://www.gebco.net/data_and_products/gridded_bathymetry_data/
- NOAA ETOPO: https://www.ncei.noaa.gov/products/etopo-global-relief-model
- ETOPO 2022 ESSD paper/license note: https://essd.copernicus.org/articles/17/1835/2025/

## 3. Atmosphere And Ocean Targets

ERA5 is the default atmospheric target. Copernicus describes ERA5 as global
hourly reanalysis from 1940 onward, updated about five days behind real time,
with 0.25 degree grids and 37 pressure levels. Copernicus Climate Data Store
access is open, free, and unrestricted, though users accept dataset licenses.

GLORYS12V1 through Copernicus Marine is the best first ocean reanalysis target:
global eddy-resolving 1/12 degree, 50 vertical levels, altimetry-era coverage
from 1993 onward, daily/monthly means, currents, sea level, temperature,
salinity, mixed-layer depth, and ice variables. Copernicus Marine describes its
service as free and open access; registration and acknowledgement still matter.

OSCAR is a useful surface-current baseline and validation product. It estimates
upper-ocean currents from satellite sea-surface height, winds, and SST gradients
using simplified geostrophic/Ekman/thermal-wind dynamics. Data.gov lists the
OSCAR final 0.25 degree product under a U.S. government works license URL.

HYCOM/NCODA provides high-resolution global ocean prediction/reanalysis access
through HYCOM servers. HYCOM states the data are UNCLASSIFIED, Distribution A,
approved for public release, with a demonstration-product disclaimer and
availability caveats. Treat HYCOM as valuable but operationally messier than
GLORYS for the first reproducible training set.

Implementation implication: train v1 on ERA5 plus GLORYS, validate against
OSCAR and PlasticAdrift, then consider HYCOM for higher-resolution or near-real
time ocean variants.

Sources:

- ERA5 climate reanalysis: https://climate.copernicus.eu/climate-reanalysis
- Copernicus Climate Data Store: https://climate.copernicus.eu/climate-data-store
- GLORYS global ocean physics reanalysis: https://data.marine.copernicus.eu/product-detail/GLOBAL_MULTIYEAR_PHY_001_030
- Copernicus Marine about/free access: https://marine.copernicus.eu/about/
- OSCAR data.gov entry: https://catalog.data.gov/dataset/ocean-surface-current-analyses-real-time-oscar-surface-currents-final-0-25-degree-version-
- ESR OSCAR overview: https://www.esr.org/data-products/oscar/oscar-surface-currents/
- HYCOM data server/disclaimer: https://www.hycom.org/
- HYCOM reanalysis acknowledgement: https://www.hycom.org/publications/acknowledgements/ocean-reanalysis-data

## 4. Neural And Operator Architectures

GraphCast proves the graph-on-sphere family for global weather. It operates on a
0.25 degree lat-long grid but uses graph neural networks and a high-resolution
multi-scale mesh representation to predict 227 atmospheric variables at 6-hour
steps. The important lesson for Fensalir is not the lat-long input. It is the
encoder/processor/decoder shape and learned multi-scale spherical mesh.

FourCastNet and SFNO prove the operator-learning family. FNOs are attractive
because operator learning can be resolution-aware, and SFNO specifically avoids
flat Fourier artifacts on spherical geometry while producing stable atmospheric
rollouts. This is relevant for global coherence and low-rank long-distance
structure.

MeshGraphNets proves learned simulation over arbitrary meshes. This maps well
to cube-sphere tiles, seam adjacency, and parent/child LOD graphs.

GenCast proves probabilistic weather through conditional diffusion and a
graph-transformer-style spherical denoiser. It produces ensemble forecasts, but
sampling is more expensive than deterministic MLWP. For Fensalir, this is a
later uncertainty-upgrade path, not the first runtime packet machine.

Implementation implication: start with a cube-sphere graph encoder plus local
multiscale tile encoder. Add SFNO/global-context distillation if local graph
context cannot capture long-range circulation. Keep diffusion/flow matching for
offline ensemble generation or teacher models until runtime cost is justified.

Sources:

- GraphCast: https://deepmind.google/research/publications/22598/
- SFNO: https://research.nvidia.com/publication/2023-06_spherical-fourier-neural-operators-learning-stable-dynamics-sphere
- FourCastNet: https://authors.library.caltech.edu/records/k959a-53q45
- NVIDIA FourCastNet V2/SFNO variables: https://docs.api.nvidia.com/nim/reference/nvidia-fourcastnet
- MeshGraphNets: https://arxiv.org/abs/2010.03409
- GenCast: https://www.nature.com/articles/s41586-024-08252-9
- NeuralGCM hybrid model context: https://www.nature.com/articles/s41586-024-07744-y

## 5. Stochastic Advection Outputs

Candidate outputs:

- mean tangent velocity: cheapest and easiest to compare with ERA5/GLORYS;
- diagonal or full diffusivity tensor: compact stochastic spread;
- transition kernel over a fixed neighbor stencil: closest to PlasticAdrift and
  best for particle/debris advection on discrete tiles;
- calibrated uncertainty/ensemble moments: needed for visible confidence and
  simulation honesty;
- seasonal/time conditioning: required because static geometry alone cannot
  determine circulation;
- source/provenance flags: needed where source data is sparse, masked, stale, or
  extrapolated.

Recommendation: first train mean velocity plus full 2x2 tangent covariance,
then add neighbor-kernel logits where transition data is available. A dense
global transition matrix is an offline research artifact, not a runtime packet.

## Short Recommendations

1. Use cube-sphere tiles as runtime authority from day one.
2. Use ETOPO as the permissive relief baseline; add GEBCO with attribution and
   TID/source-type channels.
3. Train atmosphere on ERA5 and ocean surface on GLORYS first.
4. Use OSCAR and PlasticAdrift as validation/auxiliary transport targets.
5. Treat HYCOM as a later high-resolution or near-real-time source because its
   access/disclaimer/data-gap profile is rougher.
6. Start with mean velocity plus calibrated covariance, then add transition
   kernels.
7. Use a cube-sphere graph/operator hybrid; do not force the engine to adopt a
   second spherical mesh as flow truth.
8. Keep diffusion/flow matching as an ensemble teacher or offline uncertainty
   model until the runtime cost earns its keep.
