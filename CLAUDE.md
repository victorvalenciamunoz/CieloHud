# CieloHud

App que te guía con el móvil hasta objetos del cielo (ISS, planetas, Luna) para verlos a simple vista.
Es una "gincana mirando hacia arriba": hermano de GincanaHud (mismo autor, misma estética de HUD táctico).

Lee `docs/VISION.md` para el porqué y `docs/PLAN.md` para las fases. El estado actual está en `docs/STATUS.md`.

## Principios (no negociables)

- **Cero entrada de datos manual.** La ubicación y la hora salen del dispositivo; las órbitas y efemérides son públicas.
- **Sin backend, sin base de datos.** Todo se calcula en local. Como mucho, se descargan y cachean TLEs en un fichero.
- **El cielo es el juez.** Todo cálculo debe poder contrastarse con Stellarium o Heavens-Above. Si no cuadra, está mal.
- **Fase a fase.** Trabaja SOLO en la fase actual de `docs/PLAN.md`. No adelantes trabajo de fases futuras.

## Stack

- .NET 10, C#.
- `CieloHud.Core`: librería sin dependencias de UI (se reutilizará en MAUI en la fase 3).
- `CieloHud.Console`: app de consola para validar cálculos.
- `CieloHud.Core.Tests`: xUnit.
- Fase 3 en adelante: .NET MAUI (Android-first).

## Convenciones

- Código, identificadores y comentarios en inglés. Documentación en `docs/` en español.
- Todo el tiempo interno en UTC (`DateTimeOffset`). Solo se convierte a hora local al mostrar.
- Ángulos en grados. Acimut: 0° = Norte, 90° = Este (sentido horario). Altura: 0° = horizonte, 90° = cénit.
- Tipos de valor inmutables (`record struct`) para coordenadas y observador.
- Nada de lógica de cálculo en la consola: la consola solo formatea lo que devuelve Core.

## Forma de trabajar

- Antes de añadir un paquete NuGet, explica por qué y qué alternativas has valorado. Verifica nombre y versión en NuGet; no los supongas.
- Tras cada cambio: `dotnet build` y `dotnet test`. No des un paso por terminado si algo falla.
- Al terminar un paso, actualiza `docs/STATUS.md` (qué funciona, qué falta, cómo se validó).
- Las decisiones relevantes (librerías, fórmulas, tolerancias) van a `docs/DECISIONS.md` en formato breve: contexto, decisión, alternativas.
- Pasos pequeños y verificables. Si una tarea es grande, propón cómo dividirla antes de empezar.
