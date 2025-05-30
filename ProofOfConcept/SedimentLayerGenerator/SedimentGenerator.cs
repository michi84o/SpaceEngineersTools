﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SedimentLayerGenerator
{
    // Generated by Google Gemini.
    public class SedimentGenerator
    {
        public static ushort[] GenerateSedimentLayers(Random random, int numLayers = 65535, double phaseTransitionProbability = 0.1, double hardnessAutocorrelationFactor = 0.8, double hardnessRandomVariation = 5000, double depthHardnessIncreaseFactor = 0.001)
        {
            ushort[] layers = new ushort[numLayers];

            // Phasen-Parameter
            double currentPhaseMeanHardness = 32768.0;
            double currentPhaseHardnessVariation = 10000.0;

            // Erste Schicht initialisieren
            layers[0] = (ushort)Math.Clamp((int)(currentPhaseMeanHardness + (random.NextDouble() * 2 * currentPhaseHardnessVariation - currentPhaseHardnessVariation)), 0, 65535);
            double previousHardness = layers[0];

            for (int i = 1; i < numLayers; i++)
            {
                // Phasenübergang mit Wahrscheinlichkeit
                if (random.NextDouble() < phaseTransitionProbability)
                {
                    currentPhaseMeanHardness = 32768.0 + (random.NextDouble() * 20000.0 - 10000.0);
                    currentPhaseHardnessVariation = Math.Max(1000.0, random.NextDouble() * 15000.0);
                    Console.WriteLine("Phase Transition at " + i + ": Hardness: " + currentPhaseMeanHardness + " Variation: " + currentPhaseHardnessVariation);
                }

                // Autokorrelation und zufällige Variation
                double baseHardness = previousHardness * hardnessAutocorrelationFactor + (1 - hardnessAutocorrelationFactor) * currentPhaseMeanHardness;
                double randomOffset = (random.NextDouble() * 2 * hardnessRandomVariation - hardnessRandomVariation);
                double depthOffset = i * depthHardnessIncreaseFactor;

                double newHardnessDouble = baseHardness + randomOffset + depthOffset;
                layers[i] = (ushort)Math.Clamp((int)newHardnessDouble, 0, 65535);
                previousHardness = layers[i];
            }

            return layers;
        }
    }
}
