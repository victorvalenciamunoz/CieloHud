# Avisos de terceros

El código propio de CieloHud se distribuye bajo la licencia MIT (ver [`LICENSE`](LICENSE)). Además, CieloHud incluye o usa
el siguiente software, datos y fuentes de terceros, cada uno con su propia licencia.

## Software (paquetes NuGet)

| Componente | Licencia | Origen |
|---|---|---|
| CosineKitty.AstronomyEngine 2.1.19 | MIT | https://github.com/cosinekitty/astronomy |
| SGP.NET 1.6.0 | MIT | https://github.com/parzivail/SGP.NET |
| .NET MAUI | MIT | https://github.com/dotnet/maui |
| xUnit (solo tests) | Apache 2.0 | https://github.com/xunit/xunit |

## Fuentes tipográficas (incluidas en la app)

| Fuente | Licencia | Texto de la licencia |
|---|---|---|
| Chakra Petch | SIL Open Font License 1.1 | [`licenses/OFL-ChakraPetch.txt`](licenses/OFL-ChakraPetch.txt) |
| Open Sans | SIL Open Font License 1.1 | [`licenses/OFL-OpenSans.txt`](licenses/OFL-OpenSans.txt) |

## Datos

- **Coordenadas, magnitudes y tipos espectrales de estrellas** (`src/CieloHud.Core/Stars/BrightStars.cs`): base de datos SIMBAD, CDS, Estrasburgo.
  *This research has made use of the SIMBAD database, operated at CDS, Strasbourg, France.*
- **Paralajes de estrellas** (mismo fichero): catálogo Hipparcos de la ESA, nueva reducción (van Leeuwen 2007, A&A 474, 653), catálogo I/311 de VizieR.
  *This research has made use of the VizieR catalogue access tool, CDS, Strasbourg, France (DOI: 10.26093/cds/vizier).*
- **Nombres propios de estrellas**: IAU Catalog of Star Names, IAU Division C Working Group on Star Names (WGSN),
  https://www.iau.org/public/themes/naming_stars/ (Creative Commons Attribution).
- **Límites de constelaciones**: límites oficiales de la IAU (Delporte, 1930), en la implementación de Roman (1987)
  incluida en Astronomy Engine.
- **Elementos orbitales de la ISS**: se descargan en tiempo de ejecución de CelesTrak (https://celestrak.org).
  El TLE fijo de los tests procede de CelesTrak (2026-10-01).
- **Figuras de constelaciones** (`src/CieloHud.Core/Constellations/ConstellationFigures.Data.cs`): d3-celestial,
  Copyright (c) 2015, Olaf Frohn, https://github.com/ofrohn/d3-celestial, licencia BSD de 3 cláusulas:
  [`licenses/BSD-d3-celestial.txt`](licenses/BSD-d3-celestial.txt).
