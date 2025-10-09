using Allumeria;
using Allumeria.Audio;
using Allumeria.ChunkManagement;
using Allumeria.ChunkManagement.Lighting;
using Allumeria.Input;
using Allumeria.UI;
using Allumeria.UI.Menus;
using Allumeria.UI.UINodes;
using OpenTK.Mathematics;
using SoLoud;

namespace Chart.UI
{
    public class ChartHUD : MenuController
    {
        public UIImage minimap_bg;

        public UIMapRenderer minimap;

        public UIImage minimap_marker_player;

        public UIPanel map_panel;

        public UIImage map_bg;

        public UIImage map_bg_overlay;

        public UIMapRenderer map;

        public UIImage map_marker_player;

        public UIImage map_marker_spawn;

        public bool mapOpen = false;

        protected bool dragging = false;

        public int
            dragX,
            dragY,
            mapScale = 1;

        public int
            screenPadding = 10;

        public override void BuildMenu(UINode root)
        {
            // Dark fullscreen background panel
            map_panel = new UIPanel("map_panel");

            // Fullscreen map
            map_bg = (UIImage)map_panel.RegisterNode(new UIImage("map_bg", ChartMod.texture_map, 78, 0, 138, 138));
            //map_bg_overlay = (UIImage)map_bg.RegisterNode(new UIImage("map_bg_overlay", ChartMod.texture_mapbg_overlay, 0, 0, 128, 128));
            map = (UIMapRenderer)map_bg.RegisterNode(new UIMapRenderer("map"));

            map_marker_player = (UIImage)map.RegisterNode(new UIImage("marker_player", ChartMod.texture_map, 9, 82, 9, 9));
            map_marker_player.w = 9;
            map_marker_player.h = 9;

            map_marker_spawn = (UIImage)map.RegisterNode(new UIImage("marker_spawn", ChartMod.texture_map, 0, 100, 9, 9));
            map_marker_spawn.w = 9;
            map_marker_spawn.h = 9;

            // Minimap
            minimap_bg = new UIImage("map_bg", ChartMod.texture_map, 0, 0, 78, 73);
            minimap_bg.SetSize(UIManager.scaledWidth - 78 - screenPadding, screenPadding, 78, 73);    

            minimap = (UIMapRenderer)minimap_bg.RegisterNode(new UIMapRenderer("map"));
            minimap.SetSize(screenPadding + 78 - 64, screenPadding + 73 - 64, 64, 64);
            minimap.mapW = 64;
            minimap.mapH = 64;

            minimap_marker_player = (UIImage)minimap.RegisterNode(new UIImage("map_marker", ChartMod.texture_map, 0, 73, 9, 9));
            minimap_marker_player.w = 9;
            minimap_marker_player.h = 9;

            minimap_bg.show = false;
            map_panel.show = false;
        }

        public override void Update()
        {
            show = Game.menu_HUD.show;
            minimap_bg.show = show && !mapOpen;
            map_panel.show = show && mapOpen;

            if (!show || World.player == null)
            {
                return;
            }

            // Open and close fullscreen map behavior
            if (Game.menu_HUD.chatOpen || Game.menu_HUD.radial.show || Game.menu_HUD.blockWindowOpen || World.player.inventoryOpen || Game.menu_pause.show)
            {
                mapOpen = false;
            }
            else if (!Game.paused)
            {
                if (ChartMod.input_openMap.IsPressed())
                {
                    if (mapOpen)
                    {
                        CloseMap();
                    }
                    else
                    {
                        OpenMap();
                    }
                }
            }

            (double facingY, double facingX) = Math.SinCos(Game.camera.yaw);
            (int mapX, int mapY) = ChartMod.currentMapTexture.World2Array(((int)World.player.position.X, (int)World.player.position.Z));
            (int spawnX, int spawnY) = ChartMod.currentMapTexture.World2Array((World.player.perWorldCharData.spawnPoint.X, World.player.perWorldCharData.spawnPoint.Z));

            // Minimap graphics
            minimap.mapX = mapX - 32;
            minimap.mapY = mapY - 32;

            minimap_bg.SetSize(UIManager.scaledWidth - minimap_bg.w - screenPadding, screenPadding, minimap_bg.w, minimap_bg.h);
            minimap.SetSize(minimap_bg.x + 7, minimap_bg.y + 5, 64, 64);
            
            minimap_marker_player.u = facingX > 0.3 ? 18 : facingX < -0.3 ? 0 : 9;
            minimap_marker_player.v = facingY > 0.3 ? 91 : facingY < -0.3 ? 73 : 82;

            minimap_marker_player.x = minimap.x + 28;
            minimap_marker_player.y = minimap.y + 28;

            minimap_marker_player.color = ChartMod.color_marker_player;
            //

            base.Update();
            
            //minimap.color = LightToColor(World.player.GetComponent<ModelComponent>().lightAtLocation, ((WorldRenderer)worldRendererField.GetValue(Game.game)).ambientColor);
            
            // Fullscreen map
            if (mapOpen)
            {
                // Zoom controls
                if (InputManager.GetScrollInt() == -1 && mapScale > 1)
                {
                    Zoom(mapScale / 2);
                }
                else if (InputManager.GetScrollInt() == 1 && mapScale < 8)
                {
                    Zoom(mapScale * 2);
                }

                // Dragging controls
                if (map_panel.IsMouseInBounds() && InputManager.ui_confirm.IsPressed())
                {
                    dragX = (int)(InputManager.mouseState.Position.X / UIManager.scale);
                    dragY = (int)(InputManager.mouseState.Position.Y / UIManager.scale);
                    dragging = true;
                }
                if (dragging && map_panel.IsMouseInBounds() && InputManager.ui_confirm.IsDown())
                {
                    int posX = (int)(InputManager.mouseState.Position.X / UIManager.scale);
                    int posY = (int)(InputManager.mouseState.Position.Y / UIManager.scale);

                    map.x += posX - dragX;
                    map.y += posY - dragY;

                    dragX = posX;
                    dragY = posY;
                }
                else
                {
                    dragging = false;
                }

                // Fullscreen map graphics
                map.w = ChartMod.currentMapTexture.worldWidth * mapScale / UIManager.scale;
                map.h = ChartMod.currentMapTexture.worldDepth * mapScale / UIManager.scale;

                const int dragSafetyZone = 100;

                if (map.x < -(map.w - dragSafetyZone)) map.x = -(map.w - dragSafetyZone);
                if (map.y < -(map.h - dragSafetyZone)) map.y = -(map.h - dragSafetyZone);

                if (map.x > UIManager.scaledWidth - 100) map.x = UIManager.scaledWidth - 100;
                if (map.y > UIManager.scaledHeight - 100) map.y = UIManager.scaledHeight - 100;

                map_bg.x = map.x - 40 * mapScale / UIManager.scale;
                map_bg.y = map.y - 40 * mapScale / UIManager.scale;

                map_bg.w = map.w + 80 * mapScale / UIManager.scale;
                map_bg.h = map.h + 80 * mapScale / UIManager.scale;

                //map_bg_overlay.x = map_bg.x;
                //map_bg_overlay.y = map_bg.y;
                //map_bg_overlay.w = map_bg.w;
                //map_bg_overlay.h = map_bg.h;

                map_marker_player.x = map.x + (mapX * mapScale) / UIManager.scale - 4;
                map_marker_player.y = map.y + (mapY * mapScale) / UIManager.scale - 4;

                map_marker_player.u = facingX > 0.3 ? 18 : facingX < -0.3 ? 0 : 9;
                map_marker_player.v = facingY > 0.3 ? 91 : facingY < -0.3 ? 73 : 82;

                map_marker_player.color = ChartMod.color_marker_player;

                map_marker_spawn.x = map.x + (spawnX * mapScale) / UIManager.scale - 4;
                map_marker_spawn.y = map.y + (spawnY * mapScale) / UIManager.scale - 4;

                map_marker_spawn.color = ChartMod.color_marker_spawn;
            }

            /*
            if (ChartMod.input_dumpMapCache.IsPressed())
            {
                ChartMod.mapScanner.Dump();
            }
            */
        }

        public override void Layout()
        {
            base.Layout();
        }

        public override void Render()
        {
            if (!show) return;

            if(mapOpen)
                map_panel.Render();
            else
                minimap_bg.Render();

            base.Render();
        }

        public static Vector4 LightToColor(LightValue light, Vector4 ambientColor)
        {
            Vector4 blockLight = new( light.R / 16f, light.G / 16f, light.B / 16f, 1.0f);
            Vector4 skyLight = light.S / 16f * ambientColor;
            return blockLight + skyLight;
        }

        public void CloseMap()
        {
            mapOpen = false;
            World.player.inMenu = false;
            InputManager.LockMouse();
        }

        public void OpenMap()
        {
            mapOpen = true;
            World.player.inMenu = true;
            InputManager.UnlockMouse();
            AudioPlayer.PlaySoundPlayer(ChartMod.sound_map, 1f);
        }

        public void Zoom(int newScale)
        {
            float mouseX = InputManager.mouseState.Position.X / UIManager.scale;
            float mouseY = InputManager.mouseState.Position.Y / UIManager.scale;

            float mouseOffsetX = mouseX - map.x;
            float mouseOffsetY = mouseY - map.y;

            mouseOffsetX /= mapScale;
            mouseOffsetY /= mapScale;

            mouseOffsetX *= newScale;
            mouseOffsetY *= newScale;

            map.x = (int)(mouseX - mouseOffsetX);
            map.y = (int)(mouseY - mouseOffsetY);

            mapScale = newScale;
        }
    }
}
