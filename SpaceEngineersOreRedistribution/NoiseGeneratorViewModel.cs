using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SpaceEngineersOreRedistribution
{
    internal class NoiseGeneratorViewModel : PropChangeNotifier
    {
        int _seed;
        public int Seed
        {
            get => _seed;
            set => SetProp(ref _seed, value);
        }

        int _octaves;
        public int Octaves
        {
            get => _octaves;
            set => SetProp(ref _octaves, value);
        }

        BitmapImage _image;
        public BitmapImage Image
        {
            get => _image;
            set => SetProp(ref _image, value);
        }

        public ICommand GenerateNoiseMapCommand => new RelayCommand(o =>
        {
            using (var bmap = Noise2d.GenerateNoiseMap(2048, 2048, Octaves))
            {
                Image = ImageData.FromBitmap(bmap);
            }
        });
    }
}
