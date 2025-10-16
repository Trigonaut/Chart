using Chart.UI;
using Allumeria;
using Allumeria.Blocks.BlockModels;
using Allumeria.Blocks.Blocks;
using Allumeria.Input;
using Allumeria.Rendering;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Alum.API;
using System.Collections;
using SoLoud;

namespace Chart
{
    public class ChartMod : AlumMod
    {
        public override string Name => "Chart";
        public override string Author => "Trigonaut";
        public override string Version => "0.1.2";

        public static Wav sound_map;

        public static Texture texture_map;

        public static MapTexture? currentMapTexture;

        public static MapScanner mapScanner;

        public static ChartHUD menu_map_hud;

        public static InputChannel input_openMap;

        //public static InputChannel input_dumpMapCache;

        public static Dictionary<Block, Vector3i> blockColors = [];

        public static Vector3i
            color_shade =           new(-30, -10, 10),
            color_highlight =       new(30, 10, -10),
            color_water_shallow =   new(0, 157, 237),
            color_water_deep =      new(0, 118, 198),
            color_lava =            new(221, 43, 16),
            color_oak_leaves =      new(80, 127, 62),
            color_birch_leaves =    new(178, 206, 70),
            color_maple_leaves =    new(252, 47, 55),
            color_pine_leaves =     new(26, 87, 41);

        public static Vector4
            color_marker_player = new(1.0f, 0.8f, 0.5f, 1f),
            color_marker_spawn = new(0.0f, 1.0f, 0.5f, 1f),
            color_marker_death = new(1.0f, 0.2f, 0.0f, 1f);

        public static bool blockColorsLoaded = false;

        public ChartMod()
        {
            sound_map = new Wav();
            sound_map.load("mods/Chart/res/map_sound.ogg");

            input_openMap = new("open_map", OpenTK.Windowing.GraphicsLibraryFramework.Keys.M);
            //input_dumpMapCache = new("dump_map", OpenTK.Windowing.GraphicsLibraryFramework.Keys.K);

            mapScanner = new MapScanner();

            texture_map = new Texture(Directory.GetCurrentDirectory() + "/mods/Chart/res/map.png", true, true, false, false);
        }

        public override void OnPostLoad()
        {
            menu_map_hud = (ChartHUD)Game.uiManager.RegisterMenuController(new ChartHUD(), Game.menu_HUD.panel_main);

            IList list = (IList)Alum.Alum.uiNode_nodes.GetValue(Game.uiManager.rootNode);
            list.Remove(menu_map_hud.map_panel);
            list.Insert(0, menu_map_hud.map_panel);

            Texture terrainTexture = (Texture)Alum.Alum.allumeriaAssembly.GetType("Allumeria.Rendering.Drawing").GetField("defaultTexture").GetValue(null);
            
            byte[] blockAtlasPixels = new byte[terrainTexture.size * terrainTexture.size * 4];

            terrainTexture.Use();
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, blockAtlasPixels);

            ThreadPool.QueueUserWorkItem(CalculateBlockColors, (terrainTexture.size, blockAtlasPixels));
        }

        public override void OnUpdateFrame()
        {
            if (mapScanner.updateReady)
            {
                mapScanner.UpdateTexture();
            }
        }

        public static void CalculateBlockColors(object? state)
        {
            (int size, byte[] atlasPixels) = ((int, byte[]))state;

            foreach (Block block in Block.blocks)
            {
                FaceTexture tex = block.faceTexture;
                Vector3i color;

                if (tex.umin == 0 && tex.vmin == 0)
                {
                    continue;
                }
                else
                {
                    int
                        totalRed = 0,
                        totalGreen = 0,
                        totalBlue = 0,
                        totalPixels = 0;

                    for (int x = tex.umin; x < tex.umin + 16; x++)
                    {
                        for (int y = tex.vmin; y < tex.vmin + 16; y++)
                        {
                            int index = (y * size + x) * 4;

                            byte alpha = atlasPixels[index + 3];

                            if (alpha < 255) continue;

                            totalRed += atlasPixels[index + 0];
                            totalGreen += atlasPixels[index + 1];
                            totalBlue += atlasPixels[index + 2];

                            totalPixels++;
                        }
                    }

                    if (totalPixels == 0) continue;

                    color = (totalRed / totalPixels, totalGreen / totalPixels, totalBlue / totalPixels);
                }

                blockColors.Add(block, color);
            }

            blockColors[Block.oak_leaves] = color_oak_leaves;
            blockColors[Block.birch_leaves] = color_birch_leaves;
            blockColors[Block.maple_leaves] = color_maple_leaves;
            blockColors[Block.pine_leaves] = color_pine_leaves;

            blockColorsLoaded = true;
        }

        public override void OnCreateWorld()
        {

        }
        public override void OnLoadWorld(string dir)
        {
            while (Game.worldManager.world == null)
            {
                Thread.Yield();
            }

            currentMapTexture = new MapTexture(Game.worldManager.world.worldWidth * 32, Game.worldManager.world.worldLength * 32);

            FileStream? loadMap = null;
            try
            {
                loadMap = File.OpenRead(Game.saveDirectiory + "/saves/" + dir + "/map.png");
            }
            catch (FileNotFoundException)
            {
                Logger.Info("No Chart data found in save for " + dir + ". creating new map.png");

                FileStream stream = File.Create(Game.saveDirectiory + "/saves/" + dir + "/map.png");
                new StbImageWriteSharp.ImageWriter().WritePng(new byte[currentMapTexture.worldWidth * currentMapTexture.worldDepth * 4], currentMapTexture.worldWidth, currentMapTexture.worldDepth, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
                stream.Close();

                loadMap = File.OpenRead(Game.saveDirectiory + "/saves/" + dir + "/map.png");
            }

            currentMapTexture.LoadPng(loadMap);

            Logger.Info("Loading Chart data for save " + dir);

            mapScanner.StartScanning(currentMapTexture);
        }

        public override void OnSaveWorld(string dir)
        {
            mapScanner.StopScanning();

            FileStream saveMap = File.OpenWrite(Game.saveDirectiory + "/saves/" + dir + "/map.png");
            currentMapTexture.SavePng(saveMap);

            mapScanner.StartScanning(currentMapTexture);
        }

        public override void OnQuitWorld()
        {
            mapScanner.StopScanning();
            currentMapTexture = null;
        }

        public override void OnDeleteWorld(string dir)
        {
            
        }

        public override void OnCreateCharacter()
        {
            
        }

        public override void OnLoadCharacter(string dir)
        {
            
        }

        public override void OnSaveCharacter(string dir)
        {
            
        }

        public override void OnDeleteCharacter(string dir)
        {
            
        }

        public override void OnRenderFrame()
        {
            
        }
    }
}
