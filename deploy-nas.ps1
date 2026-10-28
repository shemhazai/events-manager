#Requires -Version 7.4
<#
.SYNOPSIS
    Construit les images d'events-manager et déploie la pile sur le NAS Synology.

.DESCRIPTION
    Les trois images construites localement sont poussées directement dans le démon Docker
    du NAS par un tube SSH : aucune archive intermédiaire sur le disque, aucun registry.
    postgres:18.6 n'est pas poussée — le NAS la tire lui-même depuis Docker Hub.

    OÙ SONT LES COORDONNÉES DU NAS
    Pas ici. Adresse, port, utilisateur et chemin de clé vivent dans deploy-nas.local.psd1,
    non versionné, dont deploy-nas.local.example.psd1 est le modèle. Ce ne sont pas des secrets
    — aucune clé, aucun mot de passe — mais dans un dépôt public elles deviennent de la
    reconnaissance utile le jour où le NAS sera exposé, ce qui est l'unité suivante. Chaque
    valeur reste surchargeable par le paramètre du même nom.

    POURQUOI #Requires -Version 7.4, ET PAS 7.2 COMME LE SCRIPT DU POC
    about_Pipelines l'écrit noir sur blanc : « Before PowerShell 7.4, piping or redirecting
    output from a native program that outputs raw byte data converted the output to .NET
    strings. This conversion caused corruption of the raw data output », et « In PowerShell 7.4
    or higher, the PSNativeCommandPreserveBytePipe experimental feature is mainstream ».
    Le « docker save | ssh » de ce script en dépend entièrement. Le script du POC portait 7.2 :
    sous 7.2 ou 7.3 il aurait poussé une archive corrompue, et ne marchait que par la grâce de
    la version installée sur le poste.

    PAS DE GZIP DANS LE TUBE, ET LE MOTIF N'EST PAS LA VERSION DE DOCKER
    Mesuré le 2026-10-26 sur les trois images : 236 316 672 octets bruts contre 234 748 013
    gzippés, soit 0,66 %. La cause est le MAGASIN D'IMAGES CONTAINERD du poste, qui conserve
    les couches telles que le registre les a livrées — donc compressées — et les exporte ainsi
    (« containerd stores images in both compressed and uncompressed formats », doc Docker).
    Un poste revenu au pilote de stockage classique pourrait produire un tar NON compressé, et
    le gzip redeviendrait utile. Si le magasin change, re-mesurer avant de conclure.

    CONSTRUIRE LES TROIS EN UNE PASSE N'EST PAS COSMÉTIQUE
    api et migrate partagent l'étage « runtime-base » du Dockerfile. Construites à des moments
    différents, elles reposent sur des tirages différents de aspnet:10.0 et ne partagent alors
    AUCUNE couche : l'archive pesait 225,4 MiB. Reconstruites en une passe, elles en partagent
    8 sur 9 et l'archive tombe à 133,1 MiB. D'où le « compose build » unique ci-dessous, et le
    « docker save » unique qui porte les trois images.

    Prérequis côté NAS :
      - une règle sudoers NOPASSWD sur /usr/local/bin/docker pour l'utilisateur ;
      - le fichier .env en place dans $RemoteDir, que ce script VÉRIFIE et NE TOUCHE JAMAIS.

.EXAMPLE
    .\deploy-nas.ps1
    Construit, pousse, synchronise le compose et le seed, puis démarre la pile.

.EXAMPLE
    .\deploy-nas.ps1 -SkipBuild
    Redéploie les images locales existantes sans les reconstruire.

.EXAMPLE
    .\deploy-nas.ps1 -Migrate
    Comme ci-dessus, en appliquant les migrations avant de démarrer la pile.

.EXAMPLE
    .\deploy-nas.ps1 -NasHost nas.lan -Port 2222
    Surcharge ponctuelle de deux valeurs du fichier local.
#>
[CmdletBinding()]
param(
    [string] $NasHost,
    [int]    $Port,
    [string] $User,
    [string] $IdentityFile,
    [string] $RemoteDir = '/volume1/docker/events-manager',
    [string] $BaseUrl,
    [switch] $SkipBuild,
    [switch] $Migrate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Sans cette ligne, un code de retour non nul d'une commande native passerait inaperçu.
$PSNativeCommandUseErrorActionPreference = $true

$images = @(
    'events-manager-api:latest'
    'events-manager-web:latest'
    'events-manager-migrate:latest'
)

# Le contexte de build doit être la racine du dépôt, quel que soit le dossier courant.
Push-Location $PSScriptRoot
try {
    # ─── Coordonnées : paramètre explicite, sinon fichier local, sinon on s'arrête ──────────
    $configFile  = Join-Path $PSScriptRoot 'deploy-nas.local.psd1'
    $exampleFile = 'deploy-nas.local.example.psd1'
    $config = @{}
    if (Test-Path $configFile) {
        # Un psd1 vide, ou reduit a ses commentaires, fait rendre $null au cmdlet APRES avoir
        # ecrit une erreur — mesure le 2026-10-28. Sans ces deux gardes, le ContainsKey plus bas
        # leverait « You cannot call a method on a null-valued expression », qui ne designe ni le
        # fichier ni la cause. On rattrape pour rendre la main au message guide de plus bas.
        try {
            $loaded = Import-PowerShellDataFile -Path $configFile
        }
        catch {
            throw "Le fichier $configFile existe mais n'est pas un fichier de donnees PowerShell valide : $($_.Exception.Message) Modele : $exampleFile"
        }
        if ($null -ne $loaded) {
            $config = $loaded
        }
    }

    function Resolve-Coordinate {
        param(
            [AllowEmptyString()] [string] $Provided,
            [hashtable] $Config,
            [string] $Key
        )
        if (-not [string]::IsNullOrWhiteSpace($Provided)) {
            return $Provided
        }
        if ($Config.ContainsKey($Key) -and -not [string]::IsNullOrWhiteSpace([string]$Config[$Key])) {
            return [string]$Config[$Key]
        }
        throw "Coordonnee « $Key » introuvable. Renseignez-la dans deploy-nas.local.psd1 (modele : $exampleFile), ou passez -$Key au script."
    }

    $NasHost = Resolve-Coordinate -Provided $NasHost -Config $config -Key 'NasHost'
    $User    = Resolve-Coordinate -Provided $User    -Config $config -Key 'User'
    if ($Port -le 0) {
        $Port = [int](Resolve-Coordinate -Provided '' -Config $config -Key 'Port')
    }
    if ($Port -le 0) {
        # Le modele pose Port = 0, invalide par construction : sans ce controle, ssh recevrait
        # « -p 0 » et echouerait sur un message qui ne designe pas la cause.
        throw "Coordonnee « Port » invalide ($Port). Renseignez un port SSH valide dans deploy-nas.local.psd1 (modele : $exampleFile), ou passez -Port au script."
    }
    # ExpandEnvironmentVariables et NON une expansion de chaine PowerShell : le psd1 a ete choisi
    # parce qu'il ne porte que de la donnee, et ExpandString y reintroduirait l'evaluation de
    # $(...). Effet de bord evite au passage : un chemin contenant un « $» n'est plus altere.
    $IdentityFile = [Environment]::ExpandEnvironmentVariables(
        (Resolve-Coordinate -Provided $IdentityFile -Config $config -Key 'IdentityFile'))

    if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
        # Derive de $NasHost, et non recopie en dur : un -NasHost autre verifierait sinon la
        # mauvaise machine en silence, ce qui est exactement le genre de vert trompeur qu'on fuit.
        $BaseUrl = "http://${NasHost}:8080"
    }

    if (-not (Test-Path $IdentityFile)) {
        throw "Cle $IdentityFile absente : le deploiement ne peut pas s'authentifier."
    }
    $sshArgs = @('-p', $Port, '-i', $IdentityFile, "$User@$NasHost")

    # Chemin ABSOLU obligatoire : le secure_path de sudo n'inclut pas /usr/local/bin, et la
    # regle sudoers n'autorise que des chemins absolus.
    $composeFile = "$RemoteDir/docker-compose.yml"
    $dc = "sudo /usr/local/bin/docker compose -f $composeFile"

    if (-not $SkipBuild) {
        Write-Host '-> Build des trois images, en une passe' -ForegroundColor Cyan
        # --profile tools : sans lui, « migrate » est exclu du build, son service etant dans un
        # profil. --pull : aligne les trois sur le meme tirage des images de base.
        docker compose --profile tools build --pull
    }

    Write-Host '-> Synchronisation du compose et du seed' -ForegroundColor Cyan
    # -O force le protocole SCP historique. SANS LUI, scp passe par SFTP, que le serveur SSH
    # de DSM n'expose pas, et echoue sur « subsystem request failed on channel 0 ».
    # Le .env du NAS n'est JAMAIS synchronise : son mot de passe est fabrique sur le NAS, ne le
    # quitte jamais, et est donc different de celui du poste par construction.
    ssh @sshArgs "mkdir -p $RemoteDir/seed"
    scp -P $Port -i $IdentityFile -O docker-compose.nas.yml "${User}@${NasHost}:$composeFile"
    scp -P $Port -i $IdentityFile -O db/seed/saint-nicolas.sql "${User}@${NasHost}:$RemoteDir/seed/"

    Write-Host '-> Controle du .env sur le NAS' -ForegroundColor Cyan
    # Compose SIGNALE une variable non definie — mesure le 2026-10-28, il ecrit « The
    # "POSTGRES_PASSWORD" variable is not set. Defaulting to a blank string. » — mais il
    # N'ECHOUE PAS : il rend POSTGRES_PASSWORD: "" et poursuit. La panne n'apparait qu'ensuite,
    # quand l'image postgres refuse de s'initialiser, avec un message qui ne nomme pas la cause,
    # et l'avertissement se noie dans la sortie d'un « up ». D'ou ce controle explicite.
    # Le « && echo / || echo » garantit un code de retour nul : c'est le TEXTE qui decide.
    $envProbe = ssh @sshArgs "test -f $RemoteDir/.env && echo present || echo absent"
    if ("$envProbe".Trim() -ne 'present') {
        throw "Le fichier $RemoteDir/.env est absent sur $NasHost. Sans lui, POSTGRES_PASSWORD s'interpole en chaine vide et la base refuse de s'initialiser. Voir la section « Deploiement sur le NAS Synology » du CLAUDE.md."
    }

    Write-Host "-> Transfert des images vers $NasHost" -ForegroundColor Cyan
    # Les trois en UN SEUL save : les couches communes ne traversent le reseau qu'une fois.
    # --platform linux/amd64 n'est pas indispensable — mesure le 2026-10-28, le demon 24.0.2
    # du NAS charge aussi l'archive par defaut — mais il aplatit l'index et ecarte le manifeste
    # d'attestation « unknown/unknown » que BuildKit ajoute et dont le NAS n'a aucun usage.
    docker save --platform linux/amd64 @images | ssh @sshArgs 'sudo /usr/local/bin/docker load'

    if ($Migrate) {
        Write-Host '-> Application des migrations' -ForegroundColor Cyan
        # Geste explicite, jamais au demarrage de l'API : c'est la doctrine du depot, et c'est
        # pour cela que ce bloc est derriere un commutateur.
        # Pas de -T sur le « run » : mesure le 2026-10-28, compose v2.20.1 detecte l'absence de
        # TTY derriere un ssh non interactif et s'y adapte, pour « run » comme pour « exec ».
        ssh @sshArgs "$dc up -d db"
        ssh @sshArgs "$dc run --rm migrate"
    }

    Write-Host '-> Demarrage de la pile' -ForegroundColor Cyan
    ssh @sshArgs "$dc up -d"

    # UN CONTENEUR « Up » NE PROUVE RIEN : c'est la reponse LUE qui compte. Lecon du
    # 2026-10-18, ou une image chiseled sans tzdata rendait un 500 sur un conteneur sain.
    # On verifie DEUX chemins, parce qu'ils peuvent casser separement : /api/ traverse nginx
    # PUIS l'API, tandis que / ne sert que les statiques. Un bundle front absent passerait
    # inapercu si l'on ne lisait que l'API.
    # On n'echoue pas le deploiement si une verification rate, mais on le dit clairement.
    Write-Host "-> Verification de $BaseUrl" -ForegroundColor Cyan
    try {
        $page = Invoke-WebRequest -Uri "$BaseUrl/" -TimeoutSec 20
        if ($page.Content -match '<app-root') {
            Write-Host "OK - page HTTP $($page.StatusCode), bundle front servi" -ForegroundColor Green
        }
        else {
            Write-Host "Page HTTP $($page.StatusCode) mais sans <app-root : bundle front suspect." -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "La page $BaseUrl/ ne repond pas : $($_.Exception.Message)" -ForegroundColor Yellow
    }

    try {
        $api = Invoke-WebRequest -Uri "$BaseUrl/api/events/current" -TimeoutSec 20
        Write-Host "OK - API HTTP $($api.StatusCode)" -ForegroundColor Green
        Write-Host "     $($api.Content)" -ForegroundColor Green
    }
    catch {
        Write-Host "L'API ne rend pas 200 : $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "Hors de la fenetre de commande de l'evenement, un 404 est le comportement CORRECT." -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}
