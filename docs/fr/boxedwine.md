> [English version](../en/boxedwine.md)

# BoxedWine (chantier 13, jalon 0)

BoxedWine (`ca.kazo.boxedwine`, décision 222 A) est l'app de Kazo OS qui ouvre les programmes
Windows et DOS. Elle est l'interface d'[Os-x86](https://github.com/Kazo-ca/Os-x86), la Feature
`ca.kazo.x86` qui les fait tourner (décisions 40 et 49 A : « Os-x86 génère le rendu, BoxedWine
l'affiche »). Os-x86 n'enregistre aucune association d'extension (64) : c'est BoxedWine qui reçoit
les fichiers, et c'est Os-x86 qui décide de la machine virtuelle, du préfixe et des permissions
(193 A).

Ce document décrit ce que le chantier 13 livre (ligne 13 du plan d'exécution du jalon 0), ce qui
est vérifié et ce qui ne l'est pas.

## Ce qui est livré

| Pièce | Où | Rôle |
|---|---|---|
| App Kazo `ca.kazo.boxedwine` | `src/BoxedWine/` | fenêtre, associations, lancement, console, tutoriel |
| `BoxedWine.Lib` (`IBwLib`, `BwLib`) | `src/BoxedWine.Lib/` | logique Wine côté client : type d'un fichier, ce qui peut tourner, lancement par `Ix86` ; ne dépend que d'`Ix86` |
| Contrats d'Os-x86 | sous-module `external/Os-x86` | `OsX86.Interface` (`Ix86`), projet référencé tant qu'Os-x86 ne publie pas de paquet (question 320) |
| Kazo OS 3.5.2 | sous-module `external/KazoOs` | version du SDK, profils Debug (sources) et Release (paquets) du modèle `kazoos-solution` |
| Contournement de la permission | `build/FeaturePermissions.targets` | ajoute `x86.programs.launch` au `manifest.json` généré (question 321) |

### Le manifeste

- Genre `Window`, pas d'instance unique (184 A : une fenêtre par lancement).
- **Dépendance obligatoire** `ca.kazo.x86` à partir de 0.1.0 (ADR 0248 de Kazo) : installer
  BoxedWine installe Os-x86 avec un seul consentement ; sans Os-x86, le catalogue écarte BoxedWine
  (`catalog.dependency.missing`) ; retirer Os-x86 désactive BoxedWine.
- **Associations** (`FileHandlers`, types MIME de l'ADR 0251 §3) :

  | Extensions | Type MIME | Actions |
  |---|---|---|
  | `.exe` | `application/vnd.microsoft.portable-executable` | `open`, `execute` |
  | `.bat`, `.cmd` | `application/x-bat` | `open`, `execute` |
  | `.msi` | `application/x-msi` | `open`, `execute` |
  | `.com` | `application/x-dosexec` | `open` |

  `open` sert au double-clic, à « Ouvrir avec » et à `xdg-open`. `execute` est l'action de
  l'ADR 0251 (253 A) : un chemin tapé au shell. Kazo 3.5.2 ne la lit pas encore ; elle prendra effet
  avec la route du shell. Un programme DOS pur tapé au shell va à la commande `x86`, d'où `.com` sans
  `execute` ; au double-clic, il vient à BoxedWine.
- **Permissions** : `filesystem.user-home.read` (lire le programme), `dialogs.file-open` (« Ouvrir un
  programme… »), `filesystem.private.read` et `.write` (le réglage du tutoriel), `x86.programs.launch`
  (permission normale déclarée par Os-x86, exigée par `Ix86.RunProgramAsync`, ADR 0246).
- **Réglage** `tutorial-seen` (booléen, portée usager) dans `~/.config/ca.kazo.boxedwine/settings.config`.

### Le lancement

1. Kazo ouvre BoxedWine avec le chemin du fichier en premier argument (double-clic, « Ouvrir avec »,
   `xdg-open ./setup.exe`) ; les arguments suivants sont passés au programme.
2. BoxedWine lit le fichier par la passerelle de fichiers de l'app (Kazo vérifie la permission),
   en refusant au-delà de 256 Mio, la taille qu'Os-x86 copie dans un préfixe.
3. `IBwLib.InspectAsync` demande à Os-x86 le type du programme (`Ix86.DetectProgramAsync`, 4 Kio
   d'en-tête ; en-tête d'abord, extension ensuite, contradiction refusée : 162 A) et le classe selon
   les règles de `x86 run`.
4. Un programme qui peut tourner (console PE32 i386, .NET compris, ou fichier de commandes) est lancé
   par `IBwLib.LaunchAsync`, donc `Ix86.RunProgramAsync`, obtenu par
   `KazoFeatures.GetServiceAsync<Ix86>()` à chaque appel (ADR 0262). BoxedWine ne nomme ni la VM ni
   le préfixe : Os-x86 les choisit pour l'usager qui appelle, crée au besoin la VM `windows`, la
   démarre, télécharge l'image la première fois.
5. La console du programme s'affiche dans la fenêtre : sortie relayée en texte (séquences d'échappement
   de la console de Wine lues, jamais interprétées comme balisage), ligne tapée envoyée avec un retour
   chariot, bouton Ctrl+C (216 A), code de sortie affiché à la fin (110). **Fermer la fenêtre ferme la
   console, ce qui arrête le programme** (147) et libère la VM (166 A).

Ce qui ne peut pas encore tourner est refusé **avant** qu'Os-x86 ne démarre quoi que ce soit, avec
un message qui dit ce qu'est le fichier et ce qui viendra : programme à fenêtres, installateur
`.msi` et programme 16 bits (affichage au jalon 3), programme DOS (`x86 run` au Terminal en
attendant que BoxedWine affiche le DOS, 164 A), programme Linux (`x86 shell`), 64 bits (jamais :
v86 est 32 bits), DLL, autre processeur, fichier inconnu ou dont le contenu contredit l'extension.
Os-x86 garde le dernier mot : un refus de sa part (`program-not-supported`, budget de mémoire,
permission retirée, image absente) est affiché tel quel avec son code.

### Le tutoriel (décision 71 : obligatoire)

Au premier lancement de chaque usager, le tutoriel s'ouvre **avant** le programme demandé, qui
attend sa fermeture. Trois pages : ce qu'est BoxedWine (les programmes tournent dans une VM
d'Os-x86, pas dans Kazo), comment lancer un programme (double-clic, « Ouvrir avec », `xdg-open`,
la console, Ctrl+C, fermer arrête, premier téléchargement), et **comment ajouter soi-même ses
programmes** : pas de détection automatique ; le menu « Ajouter une application à Kazo OS »
(exécutable ou `.lnk`, entrée au menu Démarrer obligatoire, raccourci de bureau facultatif, D57)
arrive avec le jalon 1. Le bouton « Tutoriel » le rouvre. Si le réglage ne peut pas être lu ou
écrit, le tutoriel s'affiche à nouveau au lancement suivant ; rien d'autre n'est perdu.

## `IBwLib`

| Membre | Rôle |
|---|---|
| `FileTypes` (et `BwLib.HandledFileTypes`) | les cinq extensions, leur type MIME, l'action `execute` |
| `InspectAsync(nom, en-tête)` → `BwProgram` | détection par Os-x86 et `BwSupport` (ce qui peut tourner maintenant) |
| `LaunchAsync(BwLaunch)` → `IVmConsole` | lancement par `Ix86.RunProgramAsync`, refus `BwException` (`x86-missing`, `not-runnable`, `too-large`) |
| `BwLib.Classify(ProgramDetection)` | les règles de `x86 run`, sans appel |

`BwLib` reçoit une fonction qui rend `Ix86` ou null ; dans une app Kazo,
`ct => KazoFeatures.GetServiceAsync<Ix86>(ct)`. L'API publique est suivie par
`PublicAPI.Unshipped.txt`. Les commandes dans un préfixe, la lecture des `.lnk` et des icônes et la
bibliothèque viendront au jalon 1, quand `Ix86` aura « exécuter dans le préfixe ».

## Vérifié

- `dotnet build BoxedWine.slnx -c Release -warnaserror` et `-c Debug` (Kazo depuis les sources du
  sous-module) sans avertissement.
- Tests `unit` (84) : classement des programmes, lancement par un `Ix86` simulé (nom, octets,
  arguments, ni VM ni préfixe imposés, taille de la console bornée), refus avant tout démarrage,
  Os-x86 absent, console (UTF-8 coupé, retour chariot, séquences de Wine, titre sans fin, balisage
  gardé en texte, bornes), session (sortie, code de sortie, console libérée à la fin du programme,
  ligne et Ctrl+C, fenêtre fermée pendant le démarrage, fermeture qui arrête le programme, VM perdue,
  chaque échec d'Os-x86), lecture bornée du fichier, fenêtre (tutoriel avant le programme, programme
  choisi pendant le tutoriel, second programme qui ferme le premier, textes), **manifeste livré relu
  par le catalogue et le résolveur d'associations réels de Kazo 3.5.2** : les cinq extensions vont à
  BoxedWine, seul candidat, Os-x86 n'en déclare aucune, BoxedWine écarté sans Os-x86. Couverture des
  lignes : 81,6 % (BoxedWine.Lib 100 %).
- **Essai de bout en bout dans un vrai Kazo 3.5.2** (hôte de développement en Release avec la Feature
  Os-x86 de `j0/base` et BoxedWine, pack Wine 0.1.0 du chantier 10 servi en local, Chrome, session
  invitée, Kazo en français), avec la dépendance déclarée facultative **le temps de l'essai** (voir le
  second défaut de Kazo plus bas) :
  - `setup.exe` (le `osx86-hello.exe` d'Os-x86) déposé dans `/home/guest` ; **double-clic** dans
    l'Explorateur de fichiers → BoxedWine s'ouvre, tutoriel d'abord (trois pages), puis
    `GetServiceAsync<Ix86>()` rend Os-x86, la détection donne un programme console, Os-x86 crée la VM
    `windows`, télécharge le pack, démarre Wine : la fenêtre affiche
    `osx86-hello windows=C:\windows user=osx86-default args=0` et « setup.exe s'est terminé avec le
    code 7 » (environ 70 s, téléchargement compris) ;
  - `xdg-open /home/guest/setup.exe` au Terminal → BoxedWine par l'association ;
    `xdg-mime query filetype setup.exe` → `application/vnd.microsoft.portable-executable` ;
  - `boxedwine /home/guest/setup.exe --read` → seconde fenêtre (une par lancement), sans tutoriel (le
    réglage par usager est écrit), arguments transmis, une ligne tapée arrive au programme
    (`osx86-hello read bonjour Kazo`, code 0) ; le Terminal attend la fermeture de la fenêtre et rend 0 ;
  - `./setup.exe` → « Permission refusée », 126 (route du shell absente, voir plus bas) ;
  - toutes les fenêtres BoxedWine fermées, `x86 vm list` montre la VM `windows` arrêtée.

  Cet essai a trouvé deux défauts, corrigés dans la PR : la console d'un programme terminé restait
  ouverte tant que la fenêtre l'était, et gardait la VM en marche ; les textes français tutoyaient
  alors que Kazo vouvoie.

## Pas vérifié, et limites

- **Deux défauts de Kazo empêchent BoxedWine de fonctionner tel quel** (question 321), présents dans
  3.5.2, 3.6.0-preview.1, `main` et `feature/base` :
  1. le générateur de manifeste retire les dépendances avant de vérifier les permissions
     (`AppDefinitionInspector.cs`, `CheckEntryPointKinds`) et refuse la permission d'une Feature dans
     une app (KOSAPP0009) ; contournement dans ce dépôt : `build/FeaturePermissions.targets` ajoute
     `x86.programs.launch` au `manifest.json` ;
  2. au lancement, le registre paresseux construit son catalogue de découverte avec le manifeste seul
     (`BuildKnownLazyAppDescriptorRegistry.cs`, `DiscoveryManifestsOf`) : une app qui dépend
     **obligatoirement** d'une Feature est refusée (« Required dependency 'ca.kazo.x86' is missing »),
     au double-clic comme au menu, qu'elle soit intégrée à l'hôte ou installée par paquet (même
     registre). Pas de contournement dans ce dépôt : la dépendance reste obligatoire (ADR 0248) ;
     l'essai ci-dessus l'a rendue facultative localement.
- **La route du shell** : `./setup.exe` tapé au Terminal n'arrive pas encore à BoxedWine. Kazo 3.5.2
  ne publie pas le code de l'ADR 0251 : le shell refuse le fichier (« Permission refusée », 126, ou
  « cannot execute binary file » avec le bit d'exécution). BoxedWine déclare déjà l'action `execute` ;
  la démonstration « `./setup.exe` au terminal arrive dans BoxedWine » attend cette version de Kazo.
  En attendant : `xdg-open ./setup.exe` au Terminal, ou le double-clic.
- **`boxedwine setup.exe`** (163 A) : le Terminal attend la fermeture de la fenêtre, mais la console
  est dans la fenêtre, pas dans le Terminal (question 325) ; le code rendu au shell est celui de la
  fenêtre (0, 1 ou 130), pas celui du programme (limite de l'ADR 0251 §2, 110).
- **Ctrl+C** : dans l'essai, le bouton (l'octet 0x03, comme `x86 run`) n'a pas arrêté
  `osx86-hello --wait` en deux essais ; fermer la fenêtre l'a arrêté. Le pack essayé porte un agent
  plus ancien (protocole 2) que `j0/base` ; à refaire avec un pack à jour et à comparer avec `x86 run`.
- **Première ligne tapée perdue** : la première ligne envoyée juste après « reading » s'est perdue,
  la seconde est arrivée ; limite connue de la console de Wine en mode ligne (Os-x86, chantier 10).
- **Console en mode ligne** : pas d'émulateur VT (question 322) ; les programmes plein écran ne
  s'affichent pas correctement.
- **Seuls les programmes console tournent** au jalon 0 (`Ix86.RunProgramAsync`) ; le reste est
  refusé avec son message.
- Firefox, Safari et le mobile ne sont pas essayés ; l'hôte de développement de l'essai n'est pas
  dans le dépôt (il ne marche qu'avec la dépendance facultative tant que Kazo n'est pas corrigé).
