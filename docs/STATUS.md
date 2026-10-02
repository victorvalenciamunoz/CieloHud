# Estado

Fase actual: 1 (ver PLAN.md).

## Hecho

### Paso 1 — Esqueleto (2026-10-02)

- `CieloHud.slnx` con tres proyectos en .NET 10: `src/CieloHud.Core`, `src/CieloHud.Console`, `tests/CieloHud.Core.Tests`.
- `global.json` fija el SDK 10.0.401 (`rollForward: latestPatch`) para no usar el .NET 11 preview instalado en la máquina.
- `.gitignore` estándar de .NET.
- Validado con `dotnet build` (0 avisos, 0 errores) y `dotnet test` (1 test humo que carga el ensamblado de Core).

## Pendiente (Fase 1)

2. Tipos base: `Observer`, `HorizontalPosition`, `CelestialBody`, acimut → punto cardinal, con tests.
3. Sistema solar con `CosineKitty.AstronomyEngine`; tests de Luna y Júpiter.
4. Satélites con `SGP.NET`; test de la ISS con TLE fijo.
5. Proveedor de TLE (CelesTrak + caché 24 h en fichero; implementación fija para tests).
6. Consola con `--lat/--lon/--time` y tabla.
7. Validación contra Stellarium en al menos 3 instantes; resultados aquí.
