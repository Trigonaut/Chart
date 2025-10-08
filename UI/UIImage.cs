using Allumeria.Rendering;
using Allumeria.UI;
using OpenTK.Mathematics;

namespace Chart.UI
{
    public class UIImage(string internalName, Texture texture, int u, int v, int texw, int texh) : UINode(internalName)
    {
        public Texture texture = texture;
        public Vector4 color = new(1, 1, 1, 1);

        public int u = u, v = v, texw = texw, texh = texh;

        public override void Render()
        {
            TextureBatcher.batcher.Start(texture);
            TextureBatcher.batcher.AddQuadScaled(x, y, w, h, u, v, texw, texh, UIManager.scale, color);
            TextureBatcher.batcher.Finalise();
            TextureBatcher.batcher.DrawBatch();

            base.Render();
        }
    }
}
