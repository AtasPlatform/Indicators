# Changelog - CSV Levels Importer

## [1.0.0] - 2026-06-28

### Francais

Premiere version publique de `CsvLevelsImporter`, un indicateur ATAS qui lit un
fichier CSV local et affiche des niveaux horizontaux ou des zones sur le graphique.

Fonctionnalites :

- lecture de fichiers CSV separes par `;` ou `,` ;
- niveaux simples via la colonne `Price` ;
- zones via les colonnes `Price` et `Price2` ;
- labels via `Note` ;
- couleurs nommees ou hexadecimales `#RRGGBB` ;
- styles de ligne : solide, tirets, pointilles ;
- largeur de ligne configurable ;
- alignement du texte configurable ;
- rechargement automatique du fichier CSV ;
- rechargement periodique configurable ;
- affichage des erreurs sur le graphique ;
- journalisation des erreurs dans le dossier CSV.

Format CSV :

```csv
Price;Price2;Note;Color;LineType;LineWidth;TextAlign
25000;;Previous POC;gold;0;2;1
25120;25140;Volume zone;cyan;0;1;2
24950;;Support;#00FF88;1;2;0
```

### English

Initial public version of `CsvLevelsImporter`, an ATAS indicator that reads a local
CSV file and draws horizontal levels or price zones on the chart.

Features:

- reads CSV files separated by `;` or `,`;
- single levels through the `Price` column;
- zones through `Price` and `Price2`;
- labels through `Note`;
- named colors or hexadecimal `#RRGGBB` colors;
- line styles: solid, dashed, dotted;
- configurable line width;
- configurable text alignment;
- automatic CSV reload;
- configurable periodic reload;
- chart error display;
- error logging in the CSV directory.

CSV format:

```csv
Price;Price2;Note;Color;LineType;LineWidth;TextAlign
25000;;Previous POC;gold;0;2;1
25120;25140;Volume zone;cyan;0;1;2
24950;;Support;#00FF88;1;2;0
```
