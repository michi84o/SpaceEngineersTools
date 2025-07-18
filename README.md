# SpaceEngineersTools

These are my tools I wrote for Space Engineers. Most of them are for modding planets.
Here are some highlights:

* Ore Redistribution
  * Automatically generate ore patches for the blue material layer
  * Automatically regenerate biome map for the green material layer
* Planet Generator
  * Fix seams of height map tiles
  * Automatically generate height maps
  * Apply hydraulic erosion to height maps
  * Automatically or manually add lakes for the red material layer
* Complex Material Viewer
  * Autogenerate climate zones for the red material layer

## SpaceEngineersOreRedistribution:
This is a viewer for the planet definition files, specifically ore.

Default ore distribution in SE follows a fixed pattern as seen here:
![Screenshot of viewer](Screenshots/SE_Ore.png)

This is nice for new players because they can find anything they need within 5km.
I wanted to add more randomness and motivate exploration. This tool can generate new material maps with randomized ores and a XML file with the corresponding ore mappings. These files can be used to create a modded planet. Creating a mod is still a manual process.

Here is a video tutorial:
https://youtu.be/3Do1d0OU4Wg

Screenshot of a modded planet:
![Screenshot of viewer modded](Screenshots/SE_Ore_Modded.png)

The program takes a list of ore types to spawn and lets you change some variables to influence the size and depth of the ore veins. I might add additional parameters later. I need balance the probabilities a bit more.

![Screenshot of redistribution setup](Screenshots/Redistribution_Setup.png)

Currently 9-10 different ore depths will be generated and used. The veins contain more ore than in the vanilla game as a compensation for the more hard to find ore. The depths and spawn probabilities for each ore type can be edited individually.

![Screenshot of ore mappings](Screenshots/OreMappings.png)

You can also see a preview of the ore veins on the map by enabling the ore inspector and right clicking on the map.

![Screenshot of Ore Inspector](Screenshots/Ore3D.png)

## SeAdminHelper

Tools restarting and stopping an Space Engineers server.

- Watchdog: Restarts server if it is not running anymore.
- Server Stopper: Shuts down server after specified time and notifies players via ingame chat.

For more info read the README of that project.

## PlanetCreator
This is an attempt in generating procedural height maps for new planets.
The map is generated using simplex noise. After that hydraulic erosion is performed.

Area with hydralic erosion:

![Screenshot of planet creator](Screenshots/PlanetGen.png)

In a separate step, droplets are simulated to find the locally lowest points and use them to generate lakes. These lakes can be automatically added to existing material map files if the PNG folder path is supplied.
![Screenshot of planet creator](Screenshots/LakeBedFinder.jpg)

## HeightMapEdgeFixer

Exposes the edge fixer of PlanetCreator. This simple tool can be used to fix the edges of existing planet height maps like the vanilla ones. PNGs must be 16 bit grayscale and have a size of 2048x2048 pixels.

## ComplexMaterialViewer

Shows the complex material rules. Possibility to filter by latitude. I used this to create new climate zone definitions for my Seams Fixed 2.0 mod.

New since Release 1.8:
Auto generate climate zones via the menu. Open a planet, select the "DefaultSetUp" in the material groups list, then click "Generate Climate Zones." A new folder will be created, containing the new rulesets for the "ComplexMaterials" node in the planet SBC. It also generates PNGs with the corresponding red channels.

Tutorial:
https://youtu.be/nS_ERI_GFhw

![Screenshot of Complex Material Viewer](Screenshots/CpmplexMatView.png)

## SETextureEditor

Helper for editing DDS texture files (WIP).
