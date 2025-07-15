using SpaceEngineersToolsShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SETextureEditor
{
    public class TextureViewModel : PropChangeNotifier
    {
        int _width= 2048;
        public int Width
        {
            get => _width;
            set => SetProp(ref _width, value);
        }

        int _height = 2048;
        public int Height
        {
            get => _height;
            set => SetProp(ref _height, value);
        }

        WriteableBitmap _texture;
        public WriteableBitmap Texture
        {
            get => _texture;
            set
            {
                if (SetProp(ref _texture, value))
                {
                    if (value == null)
                    {
                        Width = 2048;
                        Height = 2048;
                        return;
                    }
                    else
                    {
                        Width = value.PixelWidth;
                        Height = value.PixelHeight;
                    }
                }
            }
        }

    }
}
