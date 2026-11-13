# Salon Belle Époque : jeu de coiffure (Unity 6, URP)

Jeu de salon de coiffure chic parisien, réaliste mais mignon. Il tourne sur PC, mobile et tablette.
Tout est généré par le code : le décor, les textures, les cheveux, la musique lo-fi et les bruitages.
Aucun asset payant, 0 €.

## Installation (une seule fois)

1. Dans Unity Hub, crée un projet **Universal 3D** (Unity 6 LTS), par exemple `SalonDeCoiffure`.
2. Télécharge le jeu :
   https://github.com/ljui76173-commits/Jeu-coiffure/archive/refs/heads/claude/amazing-gates-15ihhj.zip
3. Dézippe. Copie le dossier **`Salon`** dans le dossier **`Assets`** de ton projet Unity
   (par exemple `C:\Users\<toi>\SalonDeCoiffure\Assets\Salon`).
4. Reviens dans Unity et attends la fin de la compilation (barre de progression en bas à droite).
   Une fenêtre « Salon de Coiffure » s'ouvre : clique sur **Oui, préparer**.
5. Lance le jeu avec le menu **Salon de Coiffure > Jouer**, en haut de Unity.

Pour une mise à jour, retélécharge le zip et recopie le dossier `Salon` en remplaçant les fichiers.

## Nouveautés de la V1.1 (refonte)

- **Clientes sculptées** : visage d'un seul tenant (nez, lèvres, pommettes, menton), peau peinte
  (lèvres, sourcils, blush, eye-liner, taches de rousseur), yeux avec iris et cils qui suivent la caméra
  et clignent. Elle sourit ou fait la moue selon la note. Teint, yeux, maquillage et cheveux sont tirés au hasard.
- **Cheveux réalistes** : des milliers de mèches-cartes avec une texture de cheveux fins, regroupées en
  boucles naturelles. Ils encadrent le visage sans le cacher. Ils peuvent être raides, ondulés ou avec frange.
- **Commandes simples** : appuyer **sur la cliente** utilise l'outil, appuyer **à côté** tourne la vue.
  Les ciseaux coupent le long du trait dessiné. Les gestes sont environ 2 à 3 fois plus rapides qu'en V1.
- **Outils en 3D** qui suivent le doigt : ciseaux qui s'ouvrent et se ferment, tondeuse, douchette avec jet d'eau,
  flacons, pinceau teinté, séchoir.
- **Repères dorés** autour de la tête qui montrent la hauteur de coupe demandée.
- **Objectifs** qui se cochent tout seuls, et un bouton doré « Étape suivante » quand l'étape est finie.
- **Nouvelle interface** : barre d'étapes, fiche cliente avec photo, dock d'outils rond et 26 icônes dessinées.
- **Image plus belle** : tonemapping ACES, bloom doux, vignette, arrière-plan flou (profondeur de champ).

## Contenu de la V1

- Salon parisien : parquet en point de Hongrie, boiseries, miroir doré, velours, lustre, plantes, vue sur Paris.
- Bac (douchette, shampoing, rinçage, soin, serviette), coupe (ciseaux, tondeuse 9 sabots, zones),
  coloration (12 teintes, temps de pose, révélation au rinçage), séchage.
- Résultat : étoiles, avis, prix, pourboire. Photo avant/après et galerie.
- Options : nom du salon sur l'enseigne, volumes, qualité. Musique lo-fi et bruitages générés par le code.

## Feuille de route

- V2 : brushing, lisseur, fer à boucler, tresses et chignons, hommes et barbier, enfants.
- V3 : mode Gestion (argent, patience, boutique de produits, caisse).
- V4 : décoration et agrandissement du salon, employés, événements, coiffures d'époque.
- V5 : polish, optimisation mobile et tablette, builds Android / iOS / PC.

## Organisation du code

```
Salon/
  Scripts/Gen        générateurs purs (visage, corps, cheveux, icônes, textures), testables hors Unity
  Scripts/Core       SalonGame (chef d'orchestre), caméra, post-traitement, matériaux, conversions
  Scripts/World      construction du salon
  Scripts/Customers  cliente (avatar, profil, demande et notation)
  Scripts/Hair       système de mèches, mèches coupées, mousse
  Scripts/Tools      outils (douchette, ciseaux, tondeuse, pinceau…)
  Scripts/UI         interface
  Scripts/Photo      photos avant/après et galerie
  Scripts/Audio      musique et sons synthétisés
  Scripts/Economy    sauvegarde
  Shaders            shader de cheveux
  Editor             menu « Salon de Coiffure » dans Unity
```
