# Sécurité · Security

## Signaler une vulnérabilité

Merci de **ne pas ouvrir de ticket public**. Utilisez le signalement privé de GitHub :
[Signaler une vulnérabilité](https://github.com/PommeSauce0/zip-select/security/advisories/new).

Indiquez la version, le système, et si possible un ZIP ou des étapes qui reproduisent le problème. Seule la dernière version publiée reçoit des correctifs.

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub private reporting:
[Report a vulnerability](https://github.com/PommeSauce0/zip-select/security/advisories/new).

Include the version, your operating system and, if possible, a ZIP file or steps that reproduce the problem. Only the latest release receives fixes.

## Vérifier un téléchargement · Verifying a download

Chaque paquet est accompagné d’un fichier `.sha256` et d’une attestation de provenance GitHub.
Each package comes with a `.sha256` file and a GitHub build provenance attestation:

```
gh attestation verify ZIP-Select-<version>-win-x64.zip --repo PommeSauce0/zip-select
```
