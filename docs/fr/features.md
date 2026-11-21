> [English version](../en/features.md)

# Fonctionnalités de BoxedWine

| Fonctionnalité | Chantier | Décisions | État |
|---|---|---|---|
| App Kazo `ca.kazo.boxedwine`, dépendance obligatoire `ca.kazo.x86` (ADR 0248) | 13 | 222 A, 40, 49 A | livrée |
| Associations `.exe`, `.com`, `.bat`, `.cmd`, `.msi` (`open` ; `execute` sauf `.com`) | 13 | 62 B, 64, 253 A | livrée ; `execute` attend la route du shell de Kazo (ADR 0251) |
| Lancement par `Ix86` (`GetServiceAsync<Ix86>()`), VM et préfixe choisis par Os-x86 | 13 | 193 A, 162 A | livré pour les programmes console et les fichiers de commandes |
| Console dans la fenêtre : sortie, ligne tapée, Ctrl+C, code de sortie ; fermer arrête | 13 | 110, 147, 213 A, 216 A | livrée, en mode ligne |
| Tutoriel au premier lancement, rouvrable | 13 | 71 (consignes §13) | livré |
| `BoxedWine.Lib` (`IBwLib`) | 13 | plan §1.1 | livrée (type, ce qui peut tourner, lancement) |
| Licence MIT, sans code du projet Boxedwine | 13 | 47 B | livrée |
| `./setup.exe` tapé au Terminal arrive dans BoxedWine | 13 | 62 B, 240 D | attend Kazo (code de l'ADR 0251) |
| Programmes à fenêtres, installateurs, 16 bits, DOS graphique | J2, J3 | 164 A, 170 | à venir |
| « Ajouter une application à Kazo OS », bibliothèque, raccourcis, icônes | J1 | 57 A, 71 | à venir |
