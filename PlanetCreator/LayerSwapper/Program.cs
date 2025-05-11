using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace MyApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Layer Swapper for Material Maps");
            Console.WriteLine("Layer Swapper for Material Maps. Only files with 2048x2048 pixels are supported!");
            Console.WriteLine("Folder with source layers:");
            string source = Console.ReadLine();
            if (!Directory.Exists(source))
            {
                Console.WriteLine("This is not a valid directory!");
                Bye(); return;
            }
            Console.WriteLine("Folder with target layers:");
            string target = Console.ReadLine();
            if (!Directory.Exists(target))
            {
                Console.WriteLine("This is not a valid directory!");
                Bye(); return;
            }

            string[] layerFiles = new string[]
            {
                "back_mat.png",
                "down_mat.png",
                "front_mat.png",
                "left_mat.png",
                "right_mat.png",
                "up_mat.png",
            };

            foreach (string layerFile in layerFiles)
            {
                var sourcepath = Path.Combine(source, layerFile);
                var targetpath = Path.Combine(target, layerFile);
                if (!File.Exists(sourcepath))
                {
                    Console.WriteLine("File " + sourcepath + " does not exist!");
                    Bye(); return;
                }
                if (!File.Exists(targetpath))
                {
                    Console.WriteLine("File " + sourcepath + " does not exist!");
                    Bye(); return;
                }
            }

            Console.WriteLine("Which layer should be swapped?");
            Console.WriteLine("1: Lakes/Climate Zones (red)");
            Console.WriteLine("2: Biomes (green)");
            Console.WriteLine("3: Ore (blue)");
            var choice = Console.ReadLine();
            if (choice != "1" && choice != "2" && choice != "3")
            {
                Console.WriteLine("Invalid choice");
                Bye(); return;
            }
            var iChoice = int.Parse(choice);

            foreach (string layerFile in layerFiles)
            {
                var sourcepath = Path.Combine(source, layerFile);
                var targetpath = Path.Combine(target, layerFile);
                dynamic sourceImage = Image.Load(sourcepath);
                dynamic targetImage = Image.Load(targetpath);
                var targetOverride = new Image<Rgb24>(2048, 2048);
                Parallel.For(0, 2048, x =>
                {
                    for (int y = 0; y < 2048; ++y)
                    {
                        // Local image is lake only
                        var sourceImgVal = sourceImage[x, y];
                        var targetImageVal = targetImage[x, y];
                        var targetOverrideVal = new Rgb24(
                                (byte)(iChoice == 1 ? sourceImgVal.R : targetImageVal.R),
                                (byte)(iChoice == 2 ? sourceImgVal.G : targetImageVal.G),
                                (byte)(iChoice == 3 ? sourceImgVal.B : targetImageVal.B));
                        targetOverride[x, y] = targetOverrideVal;
                    }
                });
                targetOverride.Save(targetpath);
            }

            Console.WriteLine("Finished");
            Bye();
        }

        static void Bye()
        {
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}