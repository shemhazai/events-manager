# Modèle de deploy-nas.local.psd1. Copier sous ce nom (non versionné, voir .gitignore) et
# renseigner les quatre valeurs. Les quatre placeholders ci-dessous sont INVALIDES par
# construction : le script s'arrête avec un message qui nomme la clé tant qu'ils sont en place.
#
# POURQUOI CE FICHIER EXISTE
# Les coordonnées d'accès au NAS — adresse, port SSH, utilisateur, nom du fichier de clé — ne
# sont pas des secrets au sens strict : aucune clé, aucun mot de passe. Mais versionnées dans
# un dépôt public, elles deviennent de la reconnaissance utile le jour où le NAS sera exposé
# publiquement, ce qui est précisément l'unité suivante. Un port SSH décalé n'a jamais été une
# protection, mais le publier retire le peu de friction qu'il apportait. Elles vivent donc ici.
#
# Les mêmes valeurs apparaissent une seconde fois, dans .claude/settings.local.json, qui porte
# les règles de permission des commandes distantes et dont .claude/settings.local.example.json
# est le modèle. Les deux fichiers réels sont ignorés par git.
#
# SEULES CES QUATRE CLÉS SONT LUES. Ajouter RemoteDir ou BaseUrl ici n'aurait aucun effet et ne
# produirait aucun avertissement : ce sont des paramètres du script, à passer en ligne de
# commande si leur valeur par défaut ne convient pas.

@{
    # Adresse du NAS sur le réseau local, ou nom résoluble.
    NasHost = '<adresse-du-nas>'

    # Port SSH. DSM écoute sur 22 par défaut ; ce dépôt suppose un port décalé.
    # 0 est invalide : le script refuse de partir tant que la vraie valeur n'est pas posée.
    Port = 0

    # Utilisateur SSH, qui doit avoir une règle sudoers NOPASSWD sur /usr/local/bin/docker.
    User = '<utilisateur>'

    # Chemin complet de la clé privée. Les %VARIABLES% d'environnement sont développées par le
    # script, avec ExpandEnvironmentVariables et non une expansion de chaîne PowerShell : ce
    # fichier reste ainsi de la donnée pure, sans évaluation de code.
    IdentityFile = '%USERPROFILE%\.ssh\<fichier-de-cle>'
}
