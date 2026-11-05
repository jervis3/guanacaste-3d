---
version: alpha
name: "Guanacaste 3D"
description: "A dense, precise BIM workstation with an original instrument-panel identity and restrained blue context cues."
colors:
  primary: "#3298D3"
  outline: "#808080"
  surface: "#ECECEC"
  shade: "#B8B8B8"
  danger: "#E53935"
  success: "#25A85A"
  ribbon: "#F5F5F5"
  accent: "#0078D4"
  accent-soft: "#E5F3FB"
  selection: "#0078D7"
  application-background: "#F0F0F0"
  panel: "#F5F5F5"
  panel-header: "#D9D9D9"
  property-group: "#E6E6E6"
  canvas: "#FFFFFF"
  border: "#C1C1C1"
  text-primary: "#1E1E1E"
  text-secondary: "#6D6D6D"
typography:
  sans:
    fontFamily: "Segoe UI"
  interface:
    fontFamily: "Segoe UI, system-ui, sans-serif"
    fontSize: "12px"
    lineHeight: "18px"
  technical:
    fontFamily: "Segoe UI, system-ui, sans-serif"
    fontSize: "11px"
    lineHeight: "16px"
rounded:
  DEFAULT: "3px"
  compact: "2px"
  panel: "4px"
spacing:
  control-height: "24px"
  compact-gap: "4px"
  panel-inset: "8px"
components:
  ribbon:
    backgroundColor: "{colors.ribbon}"
  command-icon:
    backgroundColor: "{colors.surface}"
  command-icon-side:
    backgroundColor: "{colors.shade}"
  command-icon-delete:
    textColor: "{colors.danger}"
  command-icon-complete:
    textColor: "{colors.success}"
  button:
    radius: "3px"
  field:
    radius: "0px"
  property-row:
    minimumHeight: "24px"
  palette:
    minimumWidth: "290px"
    defaultWidth: "350px"
---

# Guanacaste 3D Design System

## Overview

### Creative North Star

The interface should feel like a well-made architectural measuring instrument: dense, calibrated, and dependable. Its information architecture follows familiar professional BIM workstations, while its visual language and assets remain original to Guanacaste 3D. The command icon reference remains the user's supplied CAD ribbon screenshots: recognizable architectural silhouettes, neutral shaded solids, fine gray outlines, blue editing marks, and compact command groups.

### Product context and register

- **Audience and primary job:** Architects, BIM coordinators, builders, and students author coordinated building models, inspect exact parameters, and navigate project views and documentation.
- **Target market and evidence:** The application is a Windows desktop technical preview for an international architecture audience. Spanish is the current product locale; no market-specific workflow is inferred from that locale.
- **Locale and language policy:** Visible copy and accessible names come from merged WPF localization dictionaries. Source identifiers, comments, diagnostics, and documentation remain English.
- **Usage scene:** Keyboard-and-mouse desktop work, often for long sessions, with high information density and repeated selection changes.
- **Register:** Product. Task clarity and stable geometry lead; branding stays in the application mark and restrained accent use.
- **Memorable signature:** A slim blue context rail and compact selection card connect the active BIM element to its editable parameters.
- **Restraint:** Property grids, the project tree, dialogs, and ribbon controls stay quiet and familiar so model data remains dominant.
- **Anti-references:** Avoid consumer-dashboard cards, oversized rounded controls, decorative gradients, floating glass surfaces, and sparse mobile-first spacing that wastes desktop working area.
- **Token ownership/runtime mapping:** This file documents the durable intent. `src/Guanacaste3D.Desktop/resources/theme.xaml` is the canonical runtime token and shared-style source. Runtime icon tokens are owned by `tools/icons/cad-icon-theme.py` and the pens emitted by `tools/icons/generate_command_icons.py`; `resources/icons.commands.xaml` is generated output. Changes to durable values must update their owning source and this file together.

## Colors

Blue is reserved for selection, focus, active context, primary confirmation, glass, and editing marks. Grey tonal layers separate title chrome, palette headers, toolbars, groups, editable white cells, and shaded model-icon faces. Red marks deletion or invalid states; green marks completion. High-contrast mode must retain system-operable focus and text rather than relying on decorative color.

## Typography

Segoe UI is the Windows-native interface family and supports the current Spanish locale. Ribbon labels remain localized text beside or beneath icons. Twelve-pixel interface text is the dense baseline; eleven-pixel text is reserved for metadata and technical hints. Use regular weight for values, semibold for active selection, and bold only for compact group headings. Numeric values remain tabular in alignment through grid geometry rather than decorative typography.

## Layout

The model canvas owns the largest area. The resizable left palette defaults to 350 pixels, never below 290 pixels, and splits vertically between Properties and Project Browser. Controls use 24-pixel rows, 4-pixel local gaps, and 8-pixel panel insets. Long property content and the project tree scroll independently so the workspace and status bar remain stable. Command artwork uses a 24 by 24 coordinate grid with fixed transparent bounds; compact ribbon widths must keep the final group reachable without changing control dimensions.

## Elevation & Depth

Hierarchy uses tonal surfaces and one-pixel borders. Model icons use pale front faces and darker side faces to communicate volume. Static product panels do not use drop shadows. Popups, dialogs, and overlays may use platform elevation supplied by WPF, but selection and focus must remain legible without shadow.

## Shapes

Controls use two- to four-pixel radii. Property cells and technical tables remain square-edged to preserve alignment. Fine square-ended gray strokes preserve the drafting character; physical objects use recognizable wall, door, column, slab, and stair silhouettes. Large pills, floating islands, and ornamental blobs do not belong in the modeling workspace.

## Components

### Foundational visual states

Every interactive control provides default, hover, keyboard-focus, pressed, selected, and disabled treatment. Selection uses blue plus readable foreground contrast. Empty and no-result states reserve the same panel geometry as populated content.

### Buttons and actions

Icon-only toolbar actions require localized accessible names and tooltips. Primary actions may use the soft accent surface; destructive actions stay separated from safe actions. Busy feedback must preserve button dimensions.

### Navigation and data display

The project browser groups views, model hierarchy, types, and documentation in an expandable tree. Search has a clear action and a visible result count. The Properties palette groups identity, instance dimensions, transforms, and openings in collapsible sections.

### Forms and overlays

Property editors use label/value rows with stable columns. Native WPF combo boxes are canonical because Windows-owned popup behavior is accepted for this desktop product. Validation must keep entered values and explain corrections in localized text.

### Iconography

Use the original vector resources in the application dictionaries and Segoe MDL2 glyphs for standard Windows chrome. Icons are optically aligned at 14 to 20 pixels; unfamiliar actions retain labels or tooltips.

Command resources are resolved by `CommandIcons.For` in ribbons, menus, and quick-access controls. Legacy `Icon.*` aliases are generated from the same artwork to keep browser and property icons aligned. Regenerate generated resources after changing artwork or its palette.

### Motion

Motion communicates expansion, selection, and workspace transitions only. Avoid ambient animation. Respect the operating system's reduced-motion preference.

### Content and data visualization

Use direct architectural vocabulary, metric units from the shared unit formatter, and action labels that state their effect. Do not embed visible copy in source code when a localization resource can own it.

## Do's and Don'ts

- **Do:** Keep active selection, property values, view navigation, and status feedback coordinated.
- **Do:** Reuse theme resources and shared controls so new tools inherit the same density and interaction states.
- **Do:** Regenerate icon resources after changing artwork or palette, and keep labels, icons, and real behavior aligned.
- **Do:** Validate all tab resources and compact ribbon access in the real WPF window.
- **Don't:** imitate proprietary icons, assets, or exact visual layouts from another BIM product.
- **Don't:** trade canvas area or scanning speed for oversized decoration.
- **Don't:** replace a functioning workflow with a decorative control or static preview.
