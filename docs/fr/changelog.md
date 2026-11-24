> [English version](../en/changelog.md)

# Journal des changements

## Non publié

- BoxedWine minimal (chantier 13 du jalon 0) : app Kazo `ca.kazo.boxedwine` 0.1.0 sur Kazo 3.5.2,
  dépendance obligatoire à la Feature `ca.kazo.x86` (ADR 0248), associations `.exe`, `.com`, `.bat`,
  `.cmd` et `.msi` (actions `open` et, sauf `.com`, `execute` de l'ADR 0251), lancement par
  `Ix86.RunProgramAsync` avec la console dans la fenêtre (sortie, ligne, Ctrl+C, code de sortie),
  refus expliqué de ce qui ne peut pas encore tourner, tutoriel au premier lancement (réglage
  `tutorial-seen` par usager).
- `BoxedWine.Lib` 0.1.0-alpha : `IBwLib`, `BwLib`, `BwFileType`, `BwProgram`, `BwSupport`, `BwLaunch`,
  `BwException`, `BwErrorCodes`.
- Contrats d'Os-x86 par le sous-module `external/Os-x86` (question 320) ; contournement
  `build/FeaturePermissions.targets` pour la permission `x86.programs.launch` (question 321).
- Licence : le dépôt passe de la GPL-2.0 à la MIT (décision 47 B) ; il ne contient aucun code du
  projet Boxedwine.
