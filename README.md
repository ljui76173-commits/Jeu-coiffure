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

## Ce que contient la V1

- **Salon parisien** : parquet en point de Hongrie, boiseries et moulures, miroir doré, console en marbre,
  fauteuils en velours, lustre, plantes, tableaux, fenêtre sur les toits de Paris avec la tour Eiffel.
- **Cliente** générée au hasard : prénom, teint, yeux, couleur et type de cheveux (raides ou ondulés),
  et une demande précise (carré, carré plongeant, pixie, dégradé, frange rideau, pointes, coloration, balayage).
- **Cheveux par mèches** : plusieurs milliers de mèches avec reflets anisotropes. Elles sont plus sombres
  et brillantes quand elles sont mouillées, et bougent légèrement.
- **Bac à shampoing** : douchette, shampoing qui mousse, rinçage, après-shampoing, serviette.
- **Coupe** : les ciseaux coupent là où tu passes, et les mèches coupées tombent au sol. La tondeuse
  a 9 sabots. Tu peux limiter l'outil à une zone (devant, côtés, dessus, nuque).
- **Coloration** : 12 teintes, peinture au pinceau mèche par mèche, temps de pose, et la couleur
  apparaît au rinçage.
- **Séchage** au sèche-cheveux (les mèches s'envolent).
- **Résultat** : note sur 5 étoiles, avis de la cliente, prix, pourboire.
- **Photo avant/après** et **galerie** des créations (export PNG).
- **Options** : nom du salon (affiché sur l'enseigne), volume, qualité. Sauvegarde automatique.
- Commandes souris **et** tactiles. Clic droit + glisser ou l'outil « Tourner la vue » pour tourner,
  molette ou les boutons pour zoomer.

## Feuille de route

- V2 : brushing, lisseur, fer à boucler, tresses et chignons, hommes et barbier, enfants.
- V3 : mode Gestion (argent, patience, boutique de produits, caisse).
- V4 : décoration et agrandissement du salon, employés, événements, coiffures d'époque.
- V5 : polish, optimisation mobile et tablette, builds Android / iOS / PC.

## Organisation du code

```
Salon/
  Scripts/Core       SalonGame (chef d'orchestre), caméra, matériaux, textures et maillages générés
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
