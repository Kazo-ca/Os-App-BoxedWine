> [English version](README.md)

# BoxedWine

BoxedWine est l'app de [Kazo OS](https://github.com/Kazo-ca/Os) qui ouvre les programmes Windows et
DOS (`.exe`, `.com`, `.bat`, `.cmd`, `.msi`). Elle est l'interface
d'[Os-x86](https://github.com/Kazo-ca/Os-x86), la fonctionnalité de Kazo OS qui les fait tourner dans
des machines virtuelles x86 dans le navigateur : double-clique un programme, BoxedWine le reçoit,
demande à Os-x86 de le lancer par `Ix86` et affiche sa console.

Elle garde le nom du projet Boxedwine mais aucun de son code : les programmes Windows tournent avec
Wine dans un Linux invité d'Os-x86, jamais avec l'émulateur Boxedwine. Sous licence MIT.

- **Jalon 0** (cette version) : programmes console et fichiers de commandes, avec leur console dans
  la fenêtre ; un tutoriel au premier lancement. Les programmes à fenêtres, les installateurs, la
  bibliothèque et les raccourcis viennent ensuite.
- Documentation : [docs/fr](docs/fr/README.md) ([ce qui est livré, vérifié et non](docs/fr/boxedwine.md)).

## Construire

```text
git clone --recurse-submodules https://github.com/Kazo-ca/Os-App-BoxedWine.git
dotnet build BoxedWine.slnx -c Release
dotnet test BoxedWine.slnx -c Release --no-build
```

`external/KazoOs` est Kazo OS sur une étiquette de version (il donne la version du SDK) ;
`external/Os-x86` est Os-x86, dont le projet `OsX86.Interface` donne `Ix86`. Debug construit Kazo OS
depuis les sources du sous-module ; Release prend les paquets de nuget.org.

## Organisation

| Chemin | Contenu |
|---|---|
| `src/BoxedWine/` | l'app Kazo `ca.kazo.boxedwine` |
| `src/BoxedWine.Lib/` | `BoxedWine.Lib` : `IBwLib` et son implémentation sur `Ix86` |
| `tests/BoxedWine.Tests/` | tests unitaires (`Category=unit`) |
| `build/` | intégration au build (contournement temporaire de la question 321) |
| `docs/fr`, `docs/en` | documentation, fonctionnalités, journal des changements |
