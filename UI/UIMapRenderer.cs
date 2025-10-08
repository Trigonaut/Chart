using Allumeria.Rendering;
using Allumeria.UI;
using OpenTK.Mathematics;

namespace Chart.UI
{
    public class UIMapRenderer(string internalName) : UINode(internalName)
    {
        public int mapX = 0, mapY = 0, mapW = 1024, mapH = 1024;
        public Vector4 color = new(1, 1, 1, 1);

        public override void Render()
        {
            if (ChartMod.currentMapTexture == null) return;
            TextureBatcher.batcher.Start(ChartMod.currentMapTexture.texture);
            TextureBatcher.batcher.AddQuadScaled(x, y, w, h, mapX, mapY, mapW, mapH, UIManager.scale, color);
            TextureBatcher.batcher.Finalise();
            TextureBatcher.batcher.DrawBatch();

            base.Render();
        }
    }
}
