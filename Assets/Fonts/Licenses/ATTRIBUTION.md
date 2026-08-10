# Icon font

Action Wheel draws its glyph icons with **CaskaydiaCove Nerd Font Propo**
(`CaskaydiaCoveNerdFontPropo-Regular.ttf`, shipped in the folder above).

The "Propo" cut specifically: in the plain cut every icon keeps the monospace advance while its ink
runs wider, so icons render off-centre to the right. Propo widens the advance to match the ink.

It is redistributed, unmodified, under the SIL Open Font License 1.1. The two licence
texts in this folder were downloaded from the projects themselves and are not edited.

## Base font

CaskaydiaCove is the Nerd Fonts build of Microsoft's **Cascadia Code**.

> Copyright (c) 2019 - Present, Microsoft Corporation, with Reserved Font Name Cascadia Code.

Cascadia Code reserves its own name under the OFL, which is why the patched build is called
*CaskaydiaCove* rather than *Cascadia Code*. Renaming the file or the family back would break that
term — leave both alone.

Full text: `Cascadia-Code-OFL-1.1.txt` — https://github.com/microsoft/cascadia-code

## Icon sets added by the patch

Nerd Fonts merges glyphs from several projects, each under its own licence. The combined statement
is in `NerdFonts-LICENSE.txt` — https://github.com/ryanoasis/nerd-fonts

The sets this app's icon picker exposes include Font Awesome, Material Design Icons, Codicons,
Devicons, Octicons, Font Logos, Weather Icons, Seti-UI and Powerline.

## Note on the logos

Some glyphs are company or product logos and remain the trademarks of their owners. They are
offered so a button can identify what it launches. That is nominative use; it does not imply any
endorsement, and it does not make the marks free to use for anything else.

## Note on the font's own metadata

The font's name table carries a stale Microsoft "supplied font" description in field 13 that
predates the OFL release and contradicts field 14, which points at the OFL. The authoritative
licence is the OFL text shipped here, taken from the Cascadia Code repository.
