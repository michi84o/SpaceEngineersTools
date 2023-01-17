using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlanetCreator
{
    class MainWindowViewModel : PropChangeNotifier
    {
        BitmapImage _tileUp;
        public BitmapImage TileUp
        {
            get => _tileUp;
            set => SetProp(ref _tileUp, value);
        }

        BitmapImage _tileFront;
        public BitmapImage TileFront
        {
            get => _tileFront;
            set => SetProp(ref _tileFront, value);
        }

        BitmapImage _tileRight;
        public BitmapImage TileRight
        {
            get => _tileRight;
            set => SetProp(ref _tileRight, value);
        }
        BitmapImage _tileBack;
        public BitmapImage TileBack
        {
            get => _tileBack;
            set => SetProp(ref _tileBack, value);
        }
        BitmapImage _tileLeft;
        public BitmapImage TileLeft
        {
            get => _tileLeft;
            set => SetProp(ref _tileLeft, value);
        }
        BitmapImage _tileDown;
        public BitmapImage TileDown
        {
            get => _tileDown;
            set => SetProp(ref _tileDown, value);
        }

        public ICommand GenerateCommand => new RelayCommand(o =>
        {
            TileUp = TileDown = TileRight = TileLeft = TileFront = TileBack = TileDown = null;

            var generator = new PlanetGenerator();
            generator.GeneratePlanet();
            LoadPictures();
        });

        public void LoadPictures()
        {
            try { TileUp = GetImage("up.png"); } catch { }
            try { TileDown = GetImage("down.png"); } catch { }
            try { TileLeft = GetImage("left.png"); } catch { }
            try { TileRight = GetImage("right.png"); } catch { }
            try { TileFront = GetImage("front.png"); } catch { }
            try { TileBack = GetImage("back.png"); } catch { }
        }

        string GetCurrentDir()
        {
            var loc = Assembly.GetExecutingAssembly().Location;
            return System.IO.Path.GetDirectoryName(loc);
        }
        string GetLocalFileName(string filename)
        {
            return System.IO.Path.Combine(GetCurrentDir(), filename);
        }
        BitmapImage GetImage(string filename)
        {
            var imageBytes = System.IO.File.ReadAllBytes(GetLocalFileName(filename)  );

            var stream = new MemoryStream(imageBytes);
            var img = new System.Windows.Media.Imaging.BitmapImage();

            img.BeginInit();
            img.StreamSource = stream;
            img.EndInit();

            return img;
        }

    }
}
