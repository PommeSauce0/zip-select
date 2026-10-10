# Historique

## 1.0.0

Première version de ZIP Select.

- Interface Avalonia en français et en anglais (menu Langue, changement immédiat et choix mémorisé) ; Windows validé, Linux expérimental.
- Extraction des fichiers correspondant à une ou plusieurs extensions, ou de tous les fichiers avec `*`.
- Recherche récursive des ZIP et choix des sous-dossiers à inclure.
- Conservation des dossiers internes ou regroupement des fichiers sans dossiers.
- Destination commune ou dossier par ZIP, qui reprend le chemin relatif de l’archive (`cours/photos.zip` → `cours/photos/`).
- Reprise sans écrasement : un fichier identique (même taille et même CRC32) est ignoré, un conflit est signalé ou renommé.
- Vérification CRC32 de chaque fichier extrait ; écriture arrêtée dès que la taille annoncée par le ZIP est dépassée.
- Noms accentués des anciens ZIP Windows (sans indicateur UTF-8) correctement décodés.
- Rejet des chemins dangereux et des liens symboliques ou jonctions dans la destination ; OneDrive et dossiers personnels déplacés acceptés.
- Fenêtre d’options avec exemples de chemins et mémorisation des choix.
- Thèmes clair et sombre, logo, progression, journal et arrêt de l’extraction.
- Exécutable Windows autonome d’environ 22 Mo.
- Scripts de compilation et de tests ; CI GitHub avec actions épinglées, attestation de provenance des paquets, Dependabot et politique de sécurité.
