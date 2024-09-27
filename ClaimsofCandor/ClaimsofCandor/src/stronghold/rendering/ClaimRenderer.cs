using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace ClaimsofCandor
{
    public class StrongholdBoundaryRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private Cuboidi highlightArea;
        private MeshRef meshRef;

        public StrongholdBoundaryRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public void SetHighlightArea(Cuboidi area)
        {
            highlightArea = area;
            UpdateMesh();
        }

        private void UpdateMesh()
        {
            if (highlightArea == null) return;

            MeshData mesh = new MeshData(24, 36);
            mesh.AddCuboid(highlightArea.MinX, highlightArea.MinY, highlightArea.MinZ, 
                           highlightArea.MaxX - highlightArea.MinX + 1, 
                           highlightArea.MaxY - highlightArea.MinY + 1, 
                           highlightArea.MaxZ - highlightArea.MinZ + 1);

            if (meshRef != null)
            {
                capi.Render.UpdateMesh(meshRef, mesh);
            }
            else
            {
                meshRef = capi.Render.UploadMesh(mesh);
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshRef == null || highlightArea == null) return;

            IRenderAPI rpi = capi.Render;
            Vec3d cameraPos = capi.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(highlightArea.Center.X, highlightArea.Center.Y, highlightArea.Center.Z);
            prog.Tex2D = capi.Render.GetTexture(new AssetLocation("game:textures/misc/boundarybox.png"));
            prog.AlphaTest = 0.05f;
            prog.AddRenderFlags = EnumRenderFlags.AlphaBlend;
            prog.RgbaLightIn = new Vec4f(1, 0, 0.25f, 0);
            prog.ModelMatrix = new float[] {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                -cameraPos.X, -cameraPos.Y, -cameraPos.Z, 1
            };

            rpi.RenderMesh(meshRef);
            prog.Stop();
        }

        public void Dispose()
        {
            if (meshRef != null)
            {
                capi.Render.DeleteMesh(meshRef);
                meshRef = null;
            }
        }
    }
}