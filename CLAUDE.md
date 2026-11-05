# CieloHud

App que te guía con el móvil hasta objetos del cielo (ISS, planetas, Luna) para verlos a simple vista.
Es una "gincana mirando hacia arriba": hermano de GincanaHud (mismo autor, misma estética de HUD táctico).

Lee `docs/VISION.md` para el porqué y `docs/PLAN.md` para las fases. El estado actual está en `docs/STATUS.md`.

Las normas comunes a los proyectos del autor (idioma, pasos, build y test, STATUS y DECISIONS, ramas y PR) están en
sus instrucciones personales (`~/.claude/CLAUDE.md`), fuera del repo. Aquí va solo lo propio de CieloHud, que es una
app MAUI:

@~/.claude/tipos/maui.md

## Principios (no negociables)

- **Cero entrada de datos manual.** La ubicación y la hora salen del dispositivo; las órbitas y efemérides son públicas.
- **Sin backend, sin base de datos.** Todo se calcula en local. Como mucho, se descargan y cachean TLEs en un fichero.
- **El cielo es el juez.** Todo cálculo debe poder contrastarse con Stellarium o Heavens-Above. Si no cuadra, está mal.

## Stack

- .NET 10, C#.
- `CieloHud.Core`: librería sin dependencias de UI.
- `CieloHud.Console`: app de consola para validar cálculos.
- `CieloHud.Core.Tests`: xUnit.
- `CieloHud.App`: app .NET MAUI (solo Android).

## Convenciones

- Ángulos en grados. Acimut: 0° = Norte, 90° = Este (sentido horario). Altura: 0° = horizonte, 90° = cénit.
- Tipos de valor inmutables (`record struct`) para coordenadas y observador.
- Nada de lógica de cálculo en la consola: la consola solo formatea lo que devuelve Core.
- Las decisiones de librerías, fórmulas y tolerancias van a `docs/DECISIONS.md`.

## Repo

El código está en `victorvalenciamunoz/CieloHud`, **público**, con `main` protegida: cada paso entra por rama y
pull request con el CI (`Tests de Core`) en verde. Las versiones se publican como releases del mismo repo. El
repo anterior, `CieloHud-codigo` (privado), queda archivado con el historial hasta el 2026-11-05.

Por ser público, nada que diga dónde vive el autor: Madrid sí (es el observador de referencia), su pueblo no; en los
textos, "casa del autor". Tampoco tablas con acimut y altura a la milésima de grado o pases con fecha, hora y altura
tomados desde casa: con ellos se deduce dónde estaba el observador; en el repo, solo las diferencias. Lo que se apoya en
esos datos vive en `../CieloHud-privado` (solo en el disco del autor): los tests se compilan si esa carpeta existe, y
`dotnet test` tiene que pasar con ella y sin ella (`CIELOHUD_PRIVATE_DIR` a una carpeta que no exista).
