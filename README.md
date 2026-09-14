# ZIP Select

[English](docs/README.en.md) · [Téléchargements](https://github.com/PommeSauce0/zip-select/releases)

Extrayez les fichiers de votre choix depuis plusieurs archives ZIP. Disponible en français et en anglais, avec thèmes clair et sombre.

![ZIP Select](docs/interface-sombre.png)

## Utilisation

Sur Windows, décompressez le téléchargement et lancez `ZIP-Select.exe`. Aucun SDK à installer.

1. Choisissez le dossier des ZIP et le dossier d’extraction.
2. Indiquez les extensions (`.mp3, .pdf`) ou `*` pour tout extraire.
3. Ajustez les options, puis cliquez sur **Extraire**.

Les options permettent de parcourir les sous-dossiers, de conserver les dossiers internes, de créer un dossier par ZIP et de renommer les conflits. Le menu **Langue** permet de passer en anglais.

Aucun fichier existant n’est écrasé. Même destination et même taille : fichier ignoré, sans comparaison du contenu. Les ZIP restent en place.

**Windows x64 testé. Linux x64 expérimental, non testé sur un bureau Linux.** Sous Linux : `chmod +x ZIP-Select`, puis `./ZIP-Select` ; glibc et un bureau X11/XWayland sont nécessaires.

## Compiler

Avec le SDK .NET 10 et PowerShell, depuis la racine :

```powershell
./scripts/Build.ps1 -Action Test
./scripts/Package.ps1 -Runtime win-x64
```

Le paquet est créé dans `dist/packages/`. Utilisez `linux-x64` pour Linux.

[Historique des versions](CHANGELOG.md) · [Logo généré par IA avec OpenAI image_gen](assets/branding/Generation.md)
