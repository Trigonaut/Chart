using Allumeria;
using Allumeria.Rendering;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;
using StbImageWriteSharp;

namespace Chart
{
    public class MapTexture
    {
        public int
            worldWidth = 1024,
            worldDepth = 1024,
            chunkSize = 32,
            //chunkCount = 32,
            scanSize = 72;

        public PixelInternalFormat pixIntern = PixelInternalFormat.Rgba8;
        public PixelFormat pixFmt = PixelFormat.Rgba;
        public PixelType pixType = PixelType.UnsignedByte;

        public Texture texture;
        public int id;

        public MapTexture(int width, int depth)
        {
            texture = new(id = GL.GenTexture())
            {
                size = Math.Max(width, depth)
            };
            GL.BindTexture(TextureTarget.Texture2D, id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, pixIntern, width, depth, 0, pixFmt, pixType, nint.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (uint)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (uint)TextureMagFilter.Nearest);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
        }

        public void LoadPng(FileStream load)
        {
            StbImage.stbi_set_flip_vertically_on_load(0);
            ImageResult image = ImageResult.FromStream(load, StbImageSharp.ColorComponents.RedGreenBlueAlpha);

            if (image.Width != worldWidth || image.Height != worldDepth)
            {
                Logger.Error("Loaded map image is the wrong size!\nExpected " + worldWidth + "x" + worldDepth + ", found " + image.Width + "x" + image.Height);
                return;
            }

            GL.BindTexture(TextureTarget.Texture2D, id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, pixIntern, worldWidth, worldDepth, 0, pixFmt, pixType, image.Data);
        }

        public void SavePng(FileStream save)
        {
            byte[] bytes = new byte[worldWidth * worldDepth * 4];
            GL.BindTexture(TextureTarget.Texture2D, id);
            GL.GetTexImage(TextureTarget.Texture2D, 0, pixFmt, pixType, bytes);
            new ImageWriter().WritePng(bytes, worldWidth, worldDepth, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, save);
        }

        public void UpdateTexture(int minX, int minZ, int maxX, int maxZ, byte[] data)
        {
            (int minU, int maxV) = Array2Image(minX, minZ);
            (int maxU, int minV) = Array2Image(maxX, maxZ);

            GL.BindTexture(TextureTarget.Texture2D, id);
            //GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, minU, minV, maxU - minU + 1, maxV - minV + 1, pixFmt, pixType, data);
            //GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        }

        public (int, int) Array2Image(int x, int y)
        {
            return (x, worldWidth - 1 - y);
        }

        public (int, int) World2Array(int x, int y)
        {
            return (x + worldWidth / 2, y + worldWidth / 2);
        }
    }
}
