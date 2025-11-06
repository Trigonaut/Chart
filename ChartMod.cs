using Chart.UI;
using Allumeria;
using Allumeria.Blocks.BlockModels;
using Allumeria.Blocks.Blocks;
using Allumeria.Input;
using Allumeria.Rendering;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Collections;
using SoLoud;
using Ignitron.Loader;
using HarmonyLib;
using Allumeria.UI;
using Allumeria.DataManagement.Saving;
using System.Text.RegularExpressions;

namespace Chart
{
    public class ChartMod : IModEntrypoint
    {
        public static ChartMod singleton;

        public static Wav sound_map;

        public static Texture texture_map;

        public static MapTexture? currentMapTexture;

        public static MapScanner mapScanner;

        public static ChartHUD menu_map_hud;

        public static InputChannel input_openMap;

        public static string loadWorld = null;
        public static bool saveWorld = false;

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

        public void Main(ModBox box)
        {
            singleton = new ChartMod();

            Logger.Info("Installed Chart");
            Harmony harmony = new("Trigonaut.Chart");
            harmony.PatchAll();


        }

        [HarmonyPatch(typeof(Game))]
        [HarmonyPatch("OnLoad")]
        public class Game_OnLoad_Patch
        {
            [HarmonyPrefix]

            public static void Prefix()
            {
                sound_map = new Wav();
                sound_map.load("mods/Chart/res/map_sound.ogg");

                input_openMap = new("open_map", OpenTK.Windowing.GraphicsLibraryFramework.Keys.M);
                //input_dumpMapCache = new("dump_map", OpenTK.Windowing.GraphicsLibraryFramework.Keys.K);

                mapScanner = new MapScanner();

                texture_map = new Texture(Directory.GetCurrentDirectory() + "/mods/Chart/res/map.png", true, true, false, false);
            }

            [HarmonyPostfix]
            public static void Postfix()
            {
                while(!Game.threadedLoadDone)
                {
                    //hang main thread until threaded loading done fuck you
                }

                menu_map_hud = (ChartHUD)Game.uiManager.RegisterMenuController(new ChartHUD(), Game.menu_HUD.panel_main);
                IList list = (IList) typeof(UINode).GetField("nodes").GetValue(Game.uiManager.rootNode);
                list.Remove(menu_map_hud.map_panel);
                list.Insert(0, menu_map_hud.map_panel);

                byte[] blockAtlasPixels = new byte[Allumeria.Rendering.Drawing.defaultTexture.size * Allumeria.Rendering.Drawing.defaultTexture.size * 4];

                GL.BindTexture(TextureTarget.Texture2D, Allumeria.Rendering.Drawing.defaultTexture.id);
                GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, blockAtlasPixels);

                ThreadPool.QueueUserWorkItem(CalculateBlockColors, (Allumeria.Rendering.Drawing.defaultTexture.size, blockAtlasPixels));
            }
        }

        [HarmonyPatch(typeof(Game))]
        [HarmonyPatch("OnUpdateFrame")]
        public class Game_Update_Patch
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                if(loadWorld != null)
                {
                    
                    using (GameSaver.saveLock.EnterScope())
                    {
                        currentMapTexture = new MapTexture(Game.worldManager.world.worldWidth * 32, Game.worldManager.world.worldLength * 32);

                        FileStream? loadMap = null;
                        try
                        {
                            loadMap = File.OpenRead(Game.saveDirectiory + "/saves/" + loadWorld + "/map.png");
                        }
                        catch (FileNotFoundException)
                        {
                            Logger.Info("No Chart data found in save for " + loadWorld + ". creating new map.png");

                            FileStream stream = File.Create(Game.saveDirectiory + "/saves/" + loadWorld + "/map.png");
                            new StbImageWriteSharp.ImageWriter().WritePng(new byte[currentMapTexture.worldWidth * currentMapTexture.worldDepth * 4], currentMapTexture.worldWidth, currentMapTexture.worldDepth, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
                            stream.Close();

                            loadMap = File.OpenRead(Game.saveDirectiory + "/saves/" + loadWorld + "/map.png");
                        }

                        currentMapTexture.LoadPng(loadMap);

                        Logger.Info("Loading Chart data for save " + loadWorld);

                        mapScanner.StartScanning(currentMapTexture);
                    }
                    loadWorld = null;
                }

                if(saveWorld)
                {
                    saveWorld = false;
                    string saveName = Game.worldManager.worldName;
                    saveName = saveName.Trim().ToLower().Replace(' ', '_');
                    saveName = Regex.Replace(saveName, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);

                    mapScanner.StopScanning();

                    FileStream saveMap = File.OpenWrite(Game.saveDirectiory + "/saves/" + saveName + "/map.png");
                    currentMapTexture.SavePng(saveMap);

                    mapScanner.StartScanning(currentMapTexture);
                }

                if (mapScanner.updateReady)
                {
                    mapScanner.UpdateTexture();
                }
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

        [HarmonyPatch(typeof(GameSaver))]
        [HarmonyPatch("SaveGame")]
        public class GameSaver_SaveGame_Patch
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                saveWorld = true;
            }
        }

        
        [HarmonyPatch(typeof(GameSaver))]
        [HarmonyPatch("LoadGame")]
        public class GameSaver_LoadGame_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(GameSaver __instance, string worldName)
            {
                loadWorld = worldName;
            }
        }
    }
}
