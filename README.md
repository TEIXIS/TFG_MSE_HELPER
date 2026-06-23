# TEA_VR / SensoryRoomHelper (tauleta)

Aplicació Unity per a la tauleta professional del projecte TEA_VR. Permet cercar i connectar-se al visor, gestionar perfils, preparar una sessió, controlar estímuls, monitoritzar l'experiència i consultar els registres guardats.

L'aplicació immersiva del visor està al repositori [TEIXIS/TFG_MSE](https://github.com/TEIXIS/TFG_MSE).

## Requisits

- Unity **6000.2.8f1** amb el mòdul **Android Build Support** (SDK, NDK i OpenJDK).
- Una tauleta Android amb depuració USB activada si es farà servir **Build And Run**.
- Un visor Meta Quest amb l'aplicació `SensoryRoom` instal·lada.
- Els dos dispositius connectats a la mateixa xarxa Wi-Fi local.

## Clonar i obrir el projecte

El projecte conserva scripts compartits dins del submòdul `Assets/Scripts/SensoryRoomCommon`. Per això el clonatge ha d'incloure submòduls:

```sh
git clone --recurse-submodules https://github.com/TEIXIS/TFG_MSE_HELPER.git
```

Si el repositori ja s'ha clonat sense submòduls, executeu:

```sh
git submodule update --init --recursive
```

Des d'Unity Hub, afegiu i obriu la carpeta arrel del repositori. Unity restaurarà els paquets definits a `Packages/manifest.json`. La versió de l'editor ha de coincidir amb `ProjectSettings/ProjectVersion.txt`.

L'escena configurada per a la compilació és `Assets/Scenes/SampleScene.unity`.

## Compilar i executar a la tauleta

1. Connecteu la tauleta per USB i accepteu la depuració USB si Android la demana.
2. A Unity, obriu **File > Build Settings** i seleccioneu **Android**. Si cal, premeu **Switch Platform**.
3. Comproveu que `SampleScene` és l'escena marcada a *Scenes In Build* i que l'arquitectura **ARM64** està activada a les opcions de Player.
4. Seleccioneu la tauleta a **Run Device** i premeu **Build And Run**.

També es pot generar un APK amb **Build** i instal·lar-lo manualment:

```sh
adb install -r <ruta-del-fitxer.apk>
```

## Connexió amb el visor

1. Connecteu la tauleta i el Meta Quest a la **mateixa xarxa Wi-Fi local**. No feu servir una xarxa de convidats, una VPN ni una configuració que aïlli els dispositius.
2. Obriu `SensoryRoom` al visor i activeu el mode amb tauleta perquè el visor comenci la descoberta LAN.
3. Obriu aquesta aplicació. La pantalla inicial cercarà visors disponibles automàticament.
4. Seleccioneu el visor detectat. La connexió TCP s'estableix i la tauleta envia el context de l'usuari seleccionat, la sala i la postura.
5. Creeu o seleccioneu un usuari i inicieu el flux de sessió: tutorial, preparació i VR.

La descoberta es fa amb UDP al port `56566`, mentre que les ordres de control circulen per TCP al port `5000`. No cal configurar manualment adreces IP.

## Ús de la sessió guiada

Des de la tauleta es pot:

- escollir usuari, tipus de sala i postura inicial;
- passar entre tutorial, preparació i VR;
- seleccionar elements i enviar-ne la configuració al visor;
- controlar llum, música, menú de mans, partícules i teletransport;
- activar el mode SOS o tancar la sessió de manera controlada;
- observar una representació simplificada de la sala i de la posició del visor quan aquest es troba en VR.

La gestió de perfils, historial i informes necessita accés a l'API configurada als clients Unity. La connexió en temps real amb el visor funciona localment dins de la xarxa Wi-Fi.

## Resolució de problemes

- **No apareix cap visor:** confirmeu la Wi-Fi compartida, obriu primer el mode amb tauleta al visor i premeu *Reintentar* a la pantalla de cerca.
- **La connexió cau en canviar temporalment d'aplicació:** torneu a portar les dues aplicacions al primer pla; les sessions no s'han de tancar només per una pausa breu de la tauleta.
- **No es guarden perfils ni sessions:** reviseu l'accés a l'API. Això és independent de la connexió local amb el visor.
- **En tancar la sessió:** feu servir el botó de tancament de la tauleta. D'aquesta manera es notifica el visor i es finalitza la persistència abans de sortir.
