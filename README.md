# SpaceEngineersTools

## SpaceEngineersOreRedistribution:
This is a viewer for the planet definition files, specifically ore.

Default ore distribution in SE follows a fixed pattern as seen here:
![Screenshot of viewer](Screenshots/SE_Ore.png)

This is nice for new players because they can find anything they need within 5km.
I wanted to add more randomness and motivate exploration. This tool can generate new material maps with randomized ores and a XML file with the corresponding ore mappings. These files can be used to create a modded planet. Creating a mod is still a manual process.

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
This is an attempt in generating procedural height maps for new planets. It's still work in progress.
The map is generated using simplex noise. After that hydraulic erosion is performed.

Area with hydralic erosion:

![Screenshot of planet creator](Screenshots/PlanetGen.png)

In a separate step, droplets are simulated to find the locally lowest points and use them to generate lakes:
This feature is not finished yet, WIP.
![Screenshot of planet creator](Screenshots/LakeBedFinder.jpg)

## HeightMapEdgeFixer

Exposes the edge fixer of PlanetCreator. This simple tool can be used to fix the edges of existing planet height maps like the vanilla ones. PNGs must be 16 bit grayscale and have a size of 2048x2048 pixels.

## ComplexMaterialViewer

Shows the complex material rules. Possibility to filter by latitude. I used this to create new climate zone definitions for my Seams Fixed 2.0 mod.

![Screenshot of Complex Material Viewer](Screenshots/CpmplexMatView.png)