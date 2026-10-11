# Noto Serif KR (subtitle font: prologue / ending only)

- `NotoSerifKR-Medium.otf`: official static Noto Serif KR Medium, v2.003 (notofonts/noto-cjk, `Serif/SubsetOTF/KR`).
  The variable `NotoSerifKR-VF.ttf` from the planning folder was not used: TextMeshPro reads only its default
  instance, which is ExtraLight (wght 200), too thin for subtitles.
- License: SIL Open Font License 1.1, see `NotoSerifKR-OFL.txt`.
- `NotoSerifKR-Medium SDF Subtitle.asset`: static TMP SDF atlas (prologue lines + `SubtitleCharacters.txt` + ASCII/punctuation).
- `NotoSerifKR-Medium SDF Dynamic Fallback.asset`: dynamic fallback from the same OTF, so edited text never shows missing-glyph boxes.
- Rebuild: `Tools > 1. Design > Rebuild Noto Serif KR Subtitle Font` (Editor/NotoSerifKRSubtitleFontBuilder.cs).
- Use it only on the prologue/ending subtitle text components. Other UI keeps the Freesentation fonts.
- Material: `_FaceDilate = -0.15` on the subtitle asset's material. The project uses Linear color space, so TMP SDF text
  renders bolder than the real font (serifs fill in and it reads as a gothic face); the negative dilate restores the true
  Noto Serif KR Medium weight. Prologue subtitle size is 38.
