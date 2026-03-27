# Design System Specification: The Fluid Professional

## 1. Overview & Creative North Star
**Creative North Star: "The Digital Glasshouse"**
This design system moves beyond the standard utility of WinUI 3 to create an editorial, high-end environment that feels like a native Windows 11 experience—elevated. We are not just building an interface; we are crafting a workspace that feels breathable, structured, and premium. 

By leveraging **intentional asymmetry** and **tonal depth**, we break the "template" look. Instead of rigid grids and heavy borders, we use the "Fluid Professional" approach: where high-contrast typography meets soft, layered surfaces. The goal is to make the user feel like they are interacting with a physical desk of frosted glass and fine paper, where hierarchy is felt through light and shadow rather than lines.

---

## 2. Colors & Surface Logic
The palette is rooted in the signature Windows Blue (`#0078D4`), but it is expanded into a sophisticated range of containers that allow for "No-Line" UI construction.

### The "No-Line" Rule
**Strict Mandate:** 1px solid borders for sectioning are prohibited. Boundaries must be defined solely through background color shifts or subtle tonal transitions.
- **Example:** A project list sitting on `surface` should use `surface-container-low` to define its area, not a stroke.

### Surface Hierarchy & Nesting
Treat the UI as a series of physical layers. Use the `surface-container` tiers to create "nested" depth:
- **Base Layer:** `surface` (#f8f9ff) / `background`.
- **Primary Layout Blocks:** `surface-container-low` (#f1f3fc).
- **Interactive Cards:** `surface-container-lowest` (#ffffff) to provide a soft, natural lift.
- **High-Emphasis Overlays:** `surface-container-high` (#e6e8f0).

### The "Glass & Gradient" Rule
To achieve a signature feel, floating elements (like the Detail Panel or Dropdowns) must use **Glassmorphism**. 
- **Recipe:** Apply 60% opacity to the `surface` token color with a `20px` to `40px` backdrop-blur.
- **Signature Textures:** Use a subtle linear gradient on primary CTAs: `primary` (#005faa) to `primary-container` (#0078d4) at a 135-degree angle. This adds "soul" and prevents the flat, "default" look.

---

## 3. Typography: Segoe UI Variable
The typography is the voice of the system. We use **Segoe UI Variable** to bridge the gap between technical precision and editorial elegance.

| Level | Token | Size | Weight | Intent |
| :--- | :--- | :--- | :--- | :--- |
| **Display** | `display-md` | 2.75rem | Semibold | Hero moments & empty states. |
| **Headline** | `headline-sm` | 1.5rem | Semibold | Section titles in the Operation Bar. |
| **Title** | `title-sm` | 1.0rem | Medium | Card headings and panel titles. |
| **Body** | `body-md` | 0.875rem | Regular | Primary data and descriptions. |
| **Label** | `label-md` | 0.75rem | Bold | Metadata, tags, and micro-copy. |

**Editorial Note:** Use `headline-sm` for page titles with a `tracking` (letter-spacing) of `-0.01em` to give it a tight, custom-tailored appearance.

---

## 4. Elevation & Depth
Depth is achieved through **Tonal Layering** and **Ambient Shadows**, never structural lines.

- **The Layering Principle:** Place a `surface-container-lowest` card (White) on a `surface-container-low` (Pale Blue-Grey) background. The 1% shift in value is enough for the human eye to perceive a boundary without a border.
- **Ambient Shadows:** For "Floating" states (Dropdowns/Detail Panels), use a shadow with a blur of `32px`, a Y-offset of `8px`, and an opacity of `6%` using the `on-surface` color. This mimics natural light.
- **The "Ghost Border" Fallback:** If a container sits on a color of the same value, use the `outline-variant` token at **15% opacity**. This provides a whisper of a boundary.

---

## 5. Components

### Side Navigation
- **Style:** Integrated into the `surface-container-low` region.
- **Active State:** A `4px` vertical "pill" of `primary` color on the left, with the menu item background shifting to `surface-container-highest`.
- **Icons:** Use thin-stroke Windows symbols. Icons should be `on-surface-variant` when inactive, and `primary` when active.

### Operation Bar & Search
- **Structure:** A horizontal strip at the top of the content area.
- **Search Input:** Use `surface-container-highest` with a `sm` (4px) corner radius. Forgo the border; use a search icon in `outline` as the placeholder.
- **Dropdowns:** Must use the "Glassmorphism" rule. Avoid hard edges; use the `DEFAULT` (8px) corner radius.

### Card-Based Project List
- **The "No-Divider" Rule:** Forbid the use of divider lines between cards. Use `spacing-4` (1rem) as a vertical gutter.
- **Card Styling:** `surface-container-lowest` background, `DEFAULT` (8px) radius. On hover, the card should transition to `secondary-container` at 30% opacity for a soft, interactive glow.

### Collapsible Detail Panel
- **Behavior:** Slides in from the right, pushing the project list (Asymmetric Layout).
- **Visuals:** Uses `surface-container-lowest` with a heavy `32px` ambient shadow. The header of the panel should use `title-lg` to create a clear entry point for the eye.

---

## 6. Do's and Don'ts

### Do
- **Do** use `spacing-6` (1.5rem) and `spacing-8` (2rem) generously to create "breathing room."
- **Do** use `surface-tint` sparingly to highlight active navigation or progress indicators.
- **Do** lean into the "Segoe UI Variable" optical sizing—ensure smaller labels use the 'Small' optical weight for legibility.

### Don't
- **Don't** use 100% black text. Always use `on-surface` (#181c22) to maintain a soft, premium feel.
- **Don't** use high-contrast shadows. If the shadow looks "dirty," the opacity is too high.
- **Don't** use 1px solid borders to separate the Operation Bar from the Project List; use a subtle background shift from `surface` to `surface-container-low`.