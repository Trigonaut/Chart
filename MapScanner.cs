using Allumeria;
using Allumeria.Blocks.Blocks;
using Allumeria.Blocks.Fluids;
using Allumeria.ChunkManagement;
using OpenTK.Mathematics;

namespace Chart
{
    public class MapScanner : IDisposable
    {
        protected Thread scanningThread;
        protected byte[] cache;
        protected MapTexture target;

        public int
            updateFrequencyMs = 100,
            minArrayX,
            minArrayZ,
            maxArrayX,
            maxArrayZ;

        public bool
            updateReady,
            waitingToTerminate = false;

        public void StartScanning(MapTexture target)
        {
            this.target = target;
            cache = new byte[target.scanSize * target.scanSize * 4];
            waitingToTerminate = false;
            scanningThread = new Thread(ThreadMethod);
            scanningThread.Start();
        }

        public void StopScanning()
        {
            if (scanningThread != null && scanningThread.IsAlive)
            {
                waitingToTerminate = true;
                scanningThread.Join();
            }
            target = null;
            cache = null;
            updateReady = false;
        }

        public void UpdateTexture()
        {
            target.UpdateTexture(minArrayX, minArrayZ, maxArrayX, maxArrayZ, cache);
            updateReady = false;
        }

        public void Dump()
        {
            StbImageWriteSharp.ImageWriter writer = new();
            FileStream fs = File.Create("dump.bmp");
            writer.WriteBmp(cache, maxArrayX - minArrayX + 1, maxArrayZ - minArrayZ + 1, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs);
            fs.Close();
            Logger.Warn("dumped map scanner cache");
        }

        protected void ThreadMethod()
        {
            while (!waitingToTerminate)
            {
                if (!updateReady && World.player != null && ChartMod.blockColorsLoaded)
                {
                    //(int colX, int colZ) = World2Col(World.player.position.X, World.player.position.Z);
                    int minWorldX = (int)World.player.position.X - target.scanSize / 2;
                    int minWorldZ = (int)World.player.position.Z - target.scanSize / 2;

                    int maxWorldX = minWorldX + target.scanSize - 1;
                    int maxWorldZ = minWorldZ + target.scanSize - 1;

                    if (maxWorldX >= target.worldWidth / 2) maxWorldX = target.worldWidth / 2 - 1;
                    if (maxWorldZ >= target.worldWidth / 2) maxWorldZ = target.worldWidth / 2 - 1;

                    if (minWorldX < -target.worldWidth / 2) minWorldX = -target.worldWidth / 2;
                    if (minWorldZ < -target.worldWidth / 2) minWorldZ = -target.worldWidth / 2;

                    updateReady = Scan(minWorldX, minWorldZ, maxWorldX, maxWorldZ);
                }

                Thread.Sleep(updateFrequencyMs);
            }
        }

        public bool Scan(int minWorldX, int minWorldZ, int maxWorldX, int maxWorldZ)
        {
            ChunkManager chunkManager = Game.worldManager.world.chunkManager;

            bool wasModified = false;

            int spanX = maxWorldX - minWorldX + 1;
            int spanZ = maxWorldZ - minWorldZ + 1;
            
            Chunk? chunk = null;

            for (int zi = spanX * (spanZ - 1), z = minWorldZ; z <= maxWorldZ; z++, zi -= spanX)
            {
                for (int xi = 0, x = minWorldX; x <= maxWorldX; x++, xi++)
                {
                    int waterDepth = 0;
                    int index = xi + zi;
                    for(int y = 191; y >= 0; y--)
                    {
                        int chunkX = x / 32;
                        int chunkY = y / 32;
                        int chunkZ = z / 32;

                        if(chunk == null || chunkX != chunk.posX || chunkY != chunk.posY || chunkZ != chunk.posZ)
                        {
                            chunk = chunkManager.RequestChunk(chunkX, chunkY, chunkZ);
                        }

                        Vector3i color = (0, 0, 0);

                        PaletteEntry block = chunkManager.GetBlockWithMetadataFast(chunk, x, y, z);
                        Block blockType = Block.blocks[block.blockID];
                        Fluid fluidType;
                        if (blockType != Block.block_empty && blockType != null)
                        {
                            if (waterDepth == 0) color = ChartMod.blockColors.GetValueOrDefault(blockType);
                            else color = ChartMod.color_water_shallow;
                        }
                        else if ((fluidType = chunkManager.GetFluid(x, y, z)) != Fluid.empty)
                        {
                            if (fluidType == Fluid.water)
                            {
                                waterDepth++;
                                if (waterDepth > 3) color = ChartMod.color_water_deep;
                                else continue;
                            }
                            else if (fluidType == Fluid.lava) color = ChartMod.color_lava;
                        }
                        else
                        {
                            continue;
                        }

                        index *= 4;
                        if (cache[index] != color.X || cache[index + 1] != color.Y || cache[index + 2] != color.Z)
                        {
                            wasModified = true;
                            cache[index] = (byte)color.X;
                            cache[index + 1] = (byte)color.Y;
                            cache[index + 2] = (byte)color.Z;
                            cache[index + 3] = 255;
                        }
                        break;
                    }
                    //int height = chunk.highestBlockHeightMap[x, z];
                    //Fluid fluid = chunkManager.GetFluid(blockX, height, blockZ);
                }
            }
            (minArrayX, minArrayZ) = target.World2Array(minWorldX, minWorldZ);
            (maxArrayX, maxArrayZ) = target.World2Array(maxWorldX, maxWorldZ);
            return wasModified;
        }

        public void Dispose()
        {
            StopScanning();
        }
    }
}
