# Graphics

---

- [Color palette](#color-palette)
  - [Design indications](#design-indications)
- [Graphic elements](#graphic-elements)
  - [`SquareLogo.svg`](#squarelogosvg)
  - [`Readme.svg`](#readmesvg)
  - [`SocialCard.svg`](#socialcardsvg)
- [Third-party material](#third-party-material)

---

## Color palette

Buildvana's graphics use a four-ink cream + espresso palette. All source SVGs draw from this palette exclusively.

| Role       | Color name    | Hex       | Swatch                                                                                                                  | Use for                                                                 |
| ---------- | ------------- | --------- | :---------------------------------------------------------------------------------------------------------------------: | ----------------------------------------------------------------------- |
| Dominant   | Deep espresso | `#2e1d17` | ![](data:image/gif;base64,R0lGODdhEAAQAIEAAC4dFwAAAAAAAAAAACwAAAAAEAAQAEAIHQABCBxIsKDBgwgTKlzIsKHDhxAjSpxIsaLFgQEBADs=) | Primary typography and elements that must carry the brand's identity.   |
| Supporting | Cream         | `#f0e6d2` | ![](data:image/gif;base64,R0lGODdhEAAQAIEAAPDm0gAAAAAAAAAAACwAAAAAEAAQAEAIHQABCBxIsKDBgwgTKlzIsKHDhxAjSpxIsaLFgQEBADs=) | Large filled regions; backgrounds; outer strokes on primary typography. |
| Accent     | Espresso      | `#5c3a2e` | ![](data:image/gif;base64,R0lGODdhEAAQAIEAAFw6LgAAAAAAAAAAACwAAAAAEAAQAEAIHQABCBxIsKDBgwgTKlzIsKHDhxAjSpxIsaLFgQEBADs=) | Secondary typography, outlines, and small details that need emphasis.   |
| Neutral    | Cream edge    | `#f8f0e0` | ![](data:image/gif;base64,R0lGODdhEAAQAIEAAPjw4AAAAAAAAAAAACwAAAAAEAAQAEAIHQABCBxIsKDBgwgTKlzIsKHDhxAjSpxIsaLFgQEBADs=) | Halos, subtle highlights, and transitional tones near Supporting areas. |

### Design indications

- **Pair colors with accessibility in mind**. According to [WebAIM's contrast checker](https://webaim.org/resources/contrastchecker):
  - Deep espresso on Cream has a 12.99:1 contrast ratio, well above WCAG AAA level for both large and normal text.
  - Espresso on Cream has a 8.07:1 contrast ratio, above WCAG AAA level for large text and WCAG AA for normal text.
- **Use `paint-order: stroke markers fill`** to produce legible text and drawings that stand out on both light and dark backgrounds from a single asset.
- **Use a transparent canvas for markdown-embedded images** so the same asset works on light-mode and dark-mode displays without needing per-theme variants.

---

## Graphic elements

All the graphic elements listed below, except where otherwise specified, are Copyright (C) Tenacom and contributors and are licensed under the MIT license. See the LICENSE file in the project root for full license information.

### `SquareLogo.svg`

Reference logo, basic square logo. Used as NuGet package icon, favicon for web-based documentation, and anywhere a square-shaped logo is needed or preferred.

This is a modifed version of [Peace](../THIRD-PARTY-GRAPHICS.md#peace). Modified by [@rdeago](https://github.com/rdeago).

Related files:

- `PackageIcon.png` (512x512px)

### `Readme.svg`

Graphic header for README file.

Uses the following material: [SquareLogo](#squarelogosvg); [Repo](../THIRD-PARTY-GRAPHICS.md#repo).

Related files:

- `Readme.png` (620x160px)

### `SocialCard.svg`

Social card for GitHub project.

Uses the following material: [SquareLogo](#squarelogosvg); [Repo](../THIRD-PARTY-GRAPHICS.md#repo); [Courier Prime](../THIRD-PARTY-GRAPHICS.md#courier-prime).

Related files:

- `SocialCard.png` (1280x640px)

---

## Third-party material

Copyright attributions for third-party graphic material are in [THIRD-PARTY-GRAPHICS.md](../THIRD-PARTY-GRAPHICS.md) in the project root.
