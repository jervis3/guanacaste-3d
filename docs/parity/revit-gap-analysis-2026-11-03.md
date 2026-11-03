# Estudio Revit ↔ Guanacaste 3D: qué falta y qué está distinto

**Fecha:** 2026-11-03 · **Revit estudiado:** 2027 (build 27.0.10.13, instalación local autorizada)
**Código estudiado:** `main` en `57578c0` (tras actualizar 9 commits).
**Herramientas:** craft-anything 0.4.0 (binario compilado, modo lectura) y REA 6.8.0 (clon actualizado).

Este documento reemplaza como fuente de verdad práctica a los estados de
[capability-catalog.md](capability-catalog.md) y
[../architecture-tool-parity-2026-11-01.md](../architecture-tool-parity-2026-11-01.md), que **están
desactualizados respecto al código** (ver §6). Mantiene la convención del programa: **Observado** (leído
directamente), **Derivado** (se sigue de lo observado), **Inferido** (plausible, sin verificar) y
**Desconocido** (requiere ensayo).

---

## 1. Resumen

1. De **277 comandos** del ribbon (que espeja el de Revit), **85 tienen manejador y 192 son botones sin
   operación detrás** (§3). Los más costosos para el uso diario son las herramientas de edición:
   Espejo, Alinear, Desfase, Matriz, Dividir, Recortar/Extender, Fijar, Copiar/Pegar y Unir geometría.
2. Hay **diferencias de comportamiento** en lo que sí está implementado, no solo ausencias (§4). Las más
   importantes: uniones de muro, línea de ubicación del muro y su efecto al cambiar el espesor del tipo,
   capas de muro, y falta de restricción de base del muro.
3. El **lado Revit de la comparación nunca se ha capturado**. Hoy solo existe la instantánea de
   Guanacaste (`ModelAudit`). Ya hay dos vías legítimas para capturarla (§5).
4. Las herramientas: **craft-anything resolvió en 2,7 s lo que REA no pudo** (corridas R3–R5 de
   [rea-runs.md](rea-runs.md)). Detalle en §2.
5. Varias capacidades listadas como «Not started» **ya existen** (§6). Conviene actualizar el catálogo
   antes de planificar para no reimplementar.

## 2. Estado de las herramientas y qué se actualizó

| Herramienta | Estado encontrado | Acción |
|---|---|---|
| REA (`C:\Dev\tools_and_scripts\rea`) | 6.8.0, atrasado frente a `origin/main` (`794907f..8c857ce`), sin compilar; Node del PATH 22.22.0 (<22.23.4 exigido); submódulos `third_party/*` sin inicializar | `git pull --ff-only` (árbol limpio) → `8c857ce`. Los 5 submódulos (binwalk, ghidra-nativeaot, jadx-headless-mcp, unblob, wakaru) **no se descargaron**: son para firmware, Android y JavaScript; el análisis .NET no los usa |
| craft-anything (`C:\Dev\craft-anything`) | Binario `craft-anything-cli.exe` 0.4.0 compilado; repositorio en la rama `bugfix/codex-account-compatibility` con cambios locales ajenos | **No se tocó el repositorio**, solo se ejecutó el binario |

**Límite de REA que sigue vigente (Observado):** el CHANGELOG de 6.8.0 no menciona el límite
«representation budget» de 64 MiB que bloqueó `inspect-managed-members` sobre `RevitAPI.dll`. Tampoco hay
Node adecuado en el PATH, y el propio proyecto indica que en Windows el addon nativo requiere el paquete npm
(ver `rea-runs.md`). No se reejecutó REA en esta sesión; sigue sirviendo para identidad, límites nativos y
grafos.

**Hallazgo (Observado):** `craft-anything-cli managed-members-list` sobre `RevitAPI.dll` con 25 tipos
filtrados terminó con cobertura `complete` en 2,7 s. Es una vía de trabajo para las firmas de la API que REA
dejó como *Unavailable*. Alcance respetado: solo firmas y tablas de metadatos, sin listados IL ni
desensamblado nativo, igual que en [evidence-log.md](evidence-log.md).

## 3. Inventario objetivo del ribbon: 192 de 277 comandos sin operación

Método: se extrajeron los ids de `controls/ribbon-catalog*.cs` y se cruzaron con los `case` de
`ExecuteApplicationCommand`, `TryExecuteStructuralCommand` y `TryExecutePresentationCommand`. Un id sin
manejador acaba en `ShowPlannedRibbonCommand` (mensaje «planificado»). Ramas que solo hacen `return false` no
cuentan como implementadas. **Nota:** `L(...)`/`S(...)` indican tamaño de botón (grande/compacto), no estado.

| Pestaña | Total | Con manejador | Sin manejador |
|---|---:|---:|---:|
| Vista | 33 | 10 | 23 |
| Anotar | 25 | 4 | 21 |
| Modificar | 21 | 1 | 20 |
| Muro (contextual) | 22 | 4 | 18 |
| Hormigón | 21 | 4 | 17 |
| Analizar | 17 | 0 | 17 |
| Gestionar | 18 | 3 | 15 |
| Sistemas | 25 | 12 | 13 |
| Colaborar | 13 | 0 | 13 |
| Insertar | 19 | 7 | 12 |
| Acero | 17 | 6 | 11 |
| Masa y emplazamiento | 16 | 7 | 9 |
| Complementos | 4 | 1 | 3 |
| Estructura | 26 | 26 | 0 |

**Verificación contra falsos positivos (Observado):** se buscó en todo `src/` cada herramienta sospechosa.
Espejo, Alinear, Matriz, Dividir, Fijar, Unir/Separar geometría, Permitir/Impedir unión y Asociar
parte superior/base **no existen en ningún archivo** salvo su botón. `Desfase` existe solo dentro de la
herramienta de dibujo de muros (`OffsetWallCenterline`), no como comando de edición.
Limitación: el cruce es estático; un manejador registrado por una vía no convencional no se vería.

El detalle por comando se regenera con `python -I tools/parity-audit/ribbon_gap.py` (acepta `--json`). Debe
volver a ejecutarse cuando cambie el ribbon o los despachadores; el total de 192 es una foto de `57578c0`.

## 4. Lo implementado que se comporta distinto a Revit

Evidencia de Revit: firmas observadas en `RevitAPI.dll`/`RevitAPI.xml` 27.0.10.13. El **comportamiento**
interno de Revit es Desconocido hasta ensayarlo; lo que sigue compara contratos públicos.

### 4.1 Uniones de muros (`wall-joins.cs`) — prioridad alta

- **Observado en código:** solo se unen extremos de línea central coincidentes (tolerancia 1e-4 m) y solo si
  hay **exactamente dos** muros en la esquina. Con tres o más, colineales o casi invertidos, quedan extremos
  planos. El tipo de unión es siempre inglete.
- **Observado en Revit:** `WallUtils.AllowWallJoinAtEnd`, `DisallowWallJoinAtEnd`, `IsWallJoinAllowedAtEnd`;
  `JoinGeometryUtils.JoinGeometry`, `UnjoinGeometry`, `SwitchJoinOrder`, `IsCuttingElementInJoin`. Es decir,
  la unión es un objeto con **permiso por extremo**, **orden** (quién corta a quién) y se aplica también a
  elementos que no son muros.
- **Brecha:** no hay forma de impedir una unión, de elegir a tope / inglete / escuadra, ni de definir
  prioridad. La herramienta de dibujo ya crea **uniones en T** (snap `Nearest`), pero el índice de uniones no
  las trata. **Inferido:** una T deja los sólidos del extremo del muro apoyado solapado o sin limpiar con el
  continuo; puede duplicar volumen en cantidades. Verificar con una prueba de volumen.

### 4.2 Línea de ubicación del muro y cambio de espesor — prioridad alta

- **Observado en código:** `WallLocationReference` (Centerline/ExteriorFace/InteriorFace) solo desplaza la
  línea central **al dibujar** (`CenterlineShift`). El `Wall` no la guarda. `UpdateWallType` reescribe
  `Wall.Thickness` de cada instancia dejando `Start/End` (línea central) fijos.
- **Derivado:** un muro dibujado por su cara exterior, al ensanchar su tipo, **mueve ambas caras** en lugar de
  conservar la cara de referencia.
- **Observado en Revit:** `CompoundStructure.GetOffsetForLocationLine` y 6 nociones de línea de ubicación
  ligadas a las capas (centro del muro/del núcleo, caras de acabado y de núcleo). **Desconocido:** que Revit
  conserve la línea elegida al cambiar el espesor; es lo esperado por diseño pero falta el ensayo (§5).
- **Riesgo adicional (Derivado):** `Thickness` existe a la vez en `Wall` y en `WallType`; dos fuentes de verdad
  que pueden divergir. Revit solo lo deriva del tipo.

### 4.3 Capas de muro — prioridad alta (hay trabajo en curso)

- **Observado en código:** las capas son un ensamblaje **fijo de 5 ranuras**
  (`LeftFine, LeftRepello, Core, RightRepello, RightFine`) derivado de `WallType` mediante
  `WallAssemblyPlanRules.For`. No hay estructura compuesta general. El propio comentario reconoce que no se
  incluyen retornos en huecos, uniones ni pintura (los retornos en huecos se añadieron después en #107/#108).
- **Observado en Revit:** `CompoundStructure` con `SetLayers`, `LayerCount`, función por capa
  (`SetLayerFunction`), material, ancho, **prioridad** (`SetLayerPriority`), núcleo
  (`GetFirstCoreLayerIndex`/`GetLastCoreLayerIndex`), **envoltura** en huecos y extremos
  (`OpeningWrapping`, `EndCap`, `ParticipatesInWrapping`), capa variable, estructura **verticalmente
  compuesta** (regiones/segmentos) y barridos de muro (`AddWallSweep`).
- **Brecha:** N capas arbitrarias con función y material; prioridad de capas en uniones (la que decide cómo se
  limpian las esquinas); envoltura de huecos y extremos; capas variables.
- **Coordinación:** existe la rama remota `feat/wall-layer-csv-ui-20261103` (exportes CSV). Antes de empezar,
  alinear con ella.

### 4.4 Restricciones verticales del muro — prioridad media

- **Observado en código:** `Wall` tiene `TopLevelId` + `TopOffset` pero **no base**: la base siempre es el
  nivel del muro. Sin `BaseOffset`, sin base asociada a otro nivel.
- **Observado en Revit:** `Wall` expone `CrossSection`, `Flipped`, `Orientation`, `StructuralUsage`,
  pila de muros (`IsStackedWall`, `GetStackedWallMemberIds`), `AddAttachment`/`RemoveAttachment`
  (asociar parte superior/base a cubiertas, forjados) y perfil editado
  (`CreateProfileSketch`, `CanHaveProfileSketch`).
- **Brecha:** desfase de base, asociar a cubierta/forjado, editar perfil (muros no rectangulares, p. ej.
  hastiales), muros apilados, sección inclinada.

### 4.5 Habitaciones — prioridad media

- **Observado en código:** la detección **ya es un grafo planar** con muros a ángulos arbitrarios, patios,
  separadores y arcos (`room-planar-boundary-detection.cs`), usando la cara del muro. Esto es mejor de lo que
  documentan los textos de paridad.
- **Observado en código:** `Room` conserva el rectángulo heredado (`FirstCorner/SecondCorner`) además de
  `Boundary/Holes`; no tiene límite superior, desfase de base ni altura sin límite.
- **Observado en Revit:** `Room` con `UpperLimit`, `BaseOffset`, `LimitOffset`, `UnboundedHeight`, `Volume`,
  `IsPointInRoom`; `SpatialElementBoundaryOptions.SpatialElementBoundaryLocation` (contorno en acabado,
  centro o núcleo) y banderas de «delimita habitación» por elemento.
- **Brecha:** volumen por altura real de habitación, opción de ubicación del contorno, flag de delimitación por
  elemento, ubicación por punto.

### 4.6 Vistas — prioridad media

- **Mejor de lo supuesto (Observado):** `ProjectView` ya tiene rango de vista (`PlanViewRange`), recorte
  (`ModelCrop`), ajustes gráficos y plantillas con gráficos.
- **Brecha (Observado):** una `ViewTemplate` solo guarda ocultos/aislados por **id de elemento** y gráficos; no
  hay visibilidad **por categoría**, overrides gráficos, filtros por reglas
  (`ParameterFilterElement`, observado en Revit), disciplina, escala ni fases.
- **Riesgo de rendimiento (Inferido):** `ViewVisibility.Apply` hace `document.Clone()` por cada aplicación;
  con documentos grandes puede costar. Medir antes de decidir.

### 4.7 Escaleras, barandillas, cortina (verificar)

- **Observado:** `Stair` = «tramo recto de escalones»; `Railing` = «protección recta con postes y dos
  pasamanos»; `Ramp`, `CurtainWall` existen. No se encontró noción de tipos de barandilla (0 coincidencias).
- **Observado en Revit:** `StairsRun` (estilo, huellas/contrahuellas reales, `LocationLineJustification`,
  tramos en espiral/croquis), `StairsLanding` (automático o croquis), soportes; `CurtainGrid` con líneas
  U/V, `Grid1/2Offset/Angle/Justification`, paneles y montantes desbloqueables.
- **Pendiente de verificar:** cuánto de rellano/tramos múltiples y de rejilla U/V editable ya cubre la rama
  actual (ARQ-09…14). Se dejó sin afirmar.

## 5. Cómo cerrar el lado Revit de la comparación (hoy ausente)

El programa nunca capturó datos de Revit para contrastar (solo `ModelAudit` de Guanacaste). Hay dos rutas
legítimas ya instaladas:

1. **El add-in propio `Guanacaste3D.RevitBridge`** (en este repo): ya exporta muros con líneas centrales,
   restricciones, materiales por capa, huecos y habitaciones desde Revit a `.g3dx`. Se puede extender para
   emitir además una instantánea comparable (`guanacaste-observation/1`).
2. **Herramientas de solo lectura del MCP oficial de Autodesk**
   («Revit 2027 MCP Server Read-Tools Technical Preview»). **Observado** (metadatos de
   `Autodesk.RevitMcpServer.Tools.dll`, 175 tipos, cobertura completa) que expone, entre otras, consultas del
   modelo con filtros (`QueryModel`), habitaciones, vistas, plantillas de vista, hojas, viewports, tablas con
   filtros y agrupación, información de proyecto, unidades, base point y vínculos. Sirve para observar, no
   para copiar implementación.

**Ensayos de caja negra propuestos** (cada uno cierra un *Desconocido* del §4):

| # | Escenario | Qué se mide |
|---|---|---|
| E1 | Muro por cara exterior; ensanchar su tipo 10 cm | Qué cara permanece; coordenadas de las 4 esquinas |
| E2 | Tres muros en un nudo y una T (extremo sobre el cuerpo de otro) | Geometría de unión; volumen total vs suma individual |
| E3 | Impedir unión en un extremo; cambiar orden de unión | Cambio de esquina y de volumen |
| E4 | Muro con 2 capas de acabado de prioridades distintas en una esquina | Qué capa corta a cuál |
| E5 | Habitación con límite superior bajo el forjado y opción de contorno «núcleo» | Área, volumen |
| E6 | Cambiar espesor del tipo con puertas alojadas | Posición y reveal de huecos |

## 6. Lo que ya existe y el catálogo da por «no iniciado»

Evidencia: tipos y archivos leídos en el código (topografía, habitaciones, cantidades por capa, tipos de
elemento) y [DEVELOPMENT_STATUS.md](../../DEVELOPMENT_STATUS.md) para eléctrica, fontanería, iluminación,
fotovoltaica e IFC; estos últimos no se volvieron a ejecutar en esta sesión. Actualizar [capability-catalog.md](capability-catalog.md): escaleras, barandillas, rampas, muro cortina,
cubiertas con pendiente por borde, cielorrasos, topografía (`geometry/topography.cs`, superficie por
splines con curvas de nivel), habitaciones por grafo planar, separadores (rectos y curvos), importación IFC,
exportes IFC/DXF/PDF/OBJ/GLB, eléctrica, fontanería y drenaje con dimensionamiento, iluminación, solar
fotovoltaica, familias `.g3df`/plantillas `.g3dt`, cantidades por capa y por hueco, transacciones y
parámetros. Esto último contradice «Not started» en las filas 7, 13 (parcial), 14 (parcial: fontanería y
eléctrica propias) y 15 (vínculos de modelos).

## 7. Backlog priorizado

Criterio: valor para diseño residencial y comercial pequeño, y dependencia técnica. Cada ítem sigue el
«incremento vertical» del [README](README.md).

| # | Ítem | Evidencia Revit | Por qué ahora |
|---|---|---|---|
| 1 | **Persistir línea de ubicación del muro** y conservarla al cambiar espesor | `GetOffsetForLocationLine` | Corrige un error de comportamiento que hoy mueve ambas caras; bajo costo |
| 2 | **Uniones de muro**: T, 3+ muros, permitir/impedir, a tope/inglete/escuadra, orden | `WallUtils.*Join*`, `JoinGeometryUtils.*` | Afecta planos y cantidades; ya hay 6 botones de unión de muro y 2 de unir geometría esperando |
| 3 | **Edición básica**: Alinear, Desfase, Espejo, Matriz, Dividir, Recortar/Extender, Fijar, Copiar/Pegar, Igualar propiedades | Modificar (20 sin manejador) | Es el uso diario; mayor ganancia de productividad |
| 4 | **Estructura compuesta** de N capas con función, material, prioridad y envoltura | `CompoundStructure.*` | Base de capas, uniones y cantidades; coordinar con la rama en curso |
| 5 | **Base del muro** (desfase/nivel) y **asociar parte superior/base** a cubierta/forjado | `Wall.AddAttachment` | Muros bajo cubiertas inclinadas |
| 6 | **Editar perfil de muro** | `Wall.CreateProfileSketch` | Hastiales, muros no rectangulares |
| 7 | **Visibilidad por categoría + filtros por reglas** en vistas | `ParameterFilterElement` | Documentación; los parámetros ya existen |
| 8 | **Cotas**: angular, radial, diámetro, longitud de arco, cota de nivel, pendiente | Anotar (21 sin manejador) | Planos de construcción |
| 9 | **Elementos de detalle**: línea, región rellena, componente, nube de revisión | `Detail*`, `RevisionCloud` | Documentación 2D |
| 10 | **Fases y opciones de diseño** | `Phase`, `DesignOption` | Remodelaciones; dependencia mayor, planificar después |
| 11 | **Parámetros compartidos, unidades de proyecto, ubicación/punto base** | Gestionar | Fundación para intercambio |

Fuera de alcance razonable por ahora: colaboración con worksets/sincronización central (el producto es
local-first), refuerzo/prefabricados, modelo analítico y mecánica (ductos, bandejas), hasta que el núcleo de
autoría esté cerrado.

## 7b. Avance tras el estudio (2026-11-03)

Cada ítem se publicó como incremento vertical (modelo, validación, deshacer, archivo, textos en español y pruebas).
Comandos del ribbon con manejador: de 85 a 105 de 277 (`tools/parity-audit/ribbon_gap.py`).

| # | Ítem | Estado | Qué falta |
|---|---|---|---|
| 1 | Línea de ubicación del muro | **Hecho** (`6fe73b8`) | Control en el panel de propiedades para ver o cambiar la cara de un muro ya dibujado |
| 2 | Uniones de muro | **Parcial** (`4aea573`): inglete, a tope, escuadra, impedir por extremo, uniones en T que siguen a su anfitrión | Orden/prioridad de unión; esquinas de 3 o más muros; elegir un solo extremo desde la interfaz (la cinta actúa en ambos) |
| 3 | Edición básica | **Parcial** (`ae9b36d`): espejo (eje = muro), desfase, matriz lineal, dividir, recortar a esquina, alinear, fijar, igualar propiedades | Matriz radial en la interfaz (existe en el Core), copiar/pegar, escala, cortar/unir geometría, medir; solo actúan sobre muros |
| 7 | Visibilidad por categoría | **Parcial** (`6c0f042`): 16 categorías por vista y plantilla, con elementos futuros incluidos | Overrides gráficos por categoría y filtros por reglas |
| 4 | Estructura compuesta de N capas | Pendiente | Coordinar con la rama `feat/wall-layer-csv-ui-20261103` |
| 5 | Base del muro y asociar parte superior/base | Pendiente | Medido: 32 usos de `wall.Height` en 18 archivos más validación de huecos; requiere un diseño transversal |
| 6, 8–11 | Perfil de muro, cotas, detalle, fases, parámetros compartidos | Pendiente | Ver §7 |

La regla que decide cuánto puede deslizarse una esquina al cambiar un espesor (`AcceptsCornerSlide` en
`wall-reference-line.cs`) tiene una versión provisional, marcada con `TODO(human)`.

## 8. Limitaciones y honestidad del estudio

- **No se ejecutó Revit** ni se hizo ningún ensayo de comportamiento; todo lo de Revit es contrato público
  (firmas y documentación). Cualquier frase sobre qué *hace* Revit está marcada Derivado/Inferido/Desconocido.
- El cruce del ribbon es **estático**; no ejecuta la aplicación. La clasificación se validó a mano en 10
  herramientas sospechosas, no en las 277.
- Las búsquedas amplias de presencia por palabra clave resultaron ruidosas y **no se usaron como evidencia**
  salvo cuando se confirmaron leyendo el código.
- REA se actualizó pero no se reejecutó; el diagnóstico de su límite de presupuesto sigue siendo el de
  `rea-runs.md`.
- El repositorio cambia deprisa (la rama de capas de muro es de hoy): verificar `main` antes de actuar.

### Reproducir

- Ribbon: `python -I tools/parity-audit/ribbon_gap.py` (Python 3, solo lectura).
- Firmas de Revit (craft-anything, solo metadatos; sustituir las rutas):

```powershell
craft-anything-cli --format json managed-members-list --path "C:\Program Files\Autodesk\Revit 2027\RevitAPI.dll" --type-names Autodesk.Revit.DB.Wall --type-names Autodesk.Revit.DB.CompoundStructure
```
