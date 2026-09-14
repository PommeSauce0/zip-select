# Publier sur GitHub

Le dépôt contient le code, les tests et la documentation. `dist/` et les sauvegardes `.backups/` sont ignorés : ne pas les ajouter avec `git add -f`.

## Première mise en ligne

1. Créer un dépôt GitHub vide avec le nom et la visibilité souhaités.
2. Vérifier `git status --short` et les fichiers ignorés avec `git status --ignored --short`.
3. Ajouter les sources et créer le premier commit.
4. Ajouter l’URL du dépôt comme remote `origin`, puis pousser la branche.
5. Attendre le workflow **Build and release** : il compile et teste Windows et Linux, puis fournit les paquets en artifacts.

## Distribuer une version

Le fichier `VERSION` pilote le numéro affiché et celui des paquets. Après vérification des tests, créer un tag correspondant, par exemple :

```sh
git tag v1.0.1
git push origin v1.0.1
```

Le workflow vérifie le tag, produit les deux paquets et leurs empreintes SHA256, puis crée une **Release en brouillon**. Relire le texte et les fichiers joints avant de publier le brouillon. Indiquer que Linux reste expérimental et non testé sur un bureau Linux tant qu’aucun essai réel ne le confirme.

Pour fabriquer les mêmes paquets localement :

```powershell
./scripts/Build.ps1 -Action Test
./scripts/Package.ps1 -Runtime win-x64
./scripts/Package.ps1 -Runtime linux-x64
```

Les fichiers à joindre sont dans `dist/packages/`. Le workflow distant ne sera vérifié qu’après la première exécution sur GitHub.
