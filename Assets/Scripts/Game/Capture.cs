using System.IO;
using UnityEngine;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// Renders the game to a PNG. Used by the automated play tests for screenshot evidence, and
    /// later for the itch.io store images.
    ///
    /// It renders the CAMERA into a RenderTexture, which includes the HUD because the HUD canvas is
    /// ScreenSpaceCamera rather than Overlay. That matters: the whole point of a screenshot as
    /// evidence is that it shows what the player sees, UI included.
    /// </summary>
    public static class Capture
    {
        public static string Root = @"D:\KT\ShiftIt\Review";

        /// <summary>Where this session's shots are going. Set once per run so a run is one folder.</summary>
        public static string Session = "";

        public static string SessionDir
        {
            get
            {
                string dir = string.IsNullOrEmpty(Session) ? Root : Path.Combine(Root, Session);
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>
        /// Luminance standard deviation of one rendered frame, 0..1.
        ///
        /// This is the bluntest and most useful check in the project: it reads the PIXELS the
        /// camera produced. A frame that is one flat colour scores ~0, which catches a dead
        /// camera, an unassigned background, a canvas that renders nowhere, or a font that
        /// failed to load - none of which any assertion about game state can see.
        /// Returns -1 if the frame could not be rendered at all.
        /// </summary>
        public static float FrameContrast(Camera cam, int width = 480, int height = 270)
        {
            if (cam == null) return -1f;
            RenderTexture rt = null;
            Texture2D tex = null;
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply(false, false);

                var px = tex.GetPixels32();
                double sum = 0.0, sumSq = 0.0;
                for (int i = 0; i < px.Length; i++)
                {
                    double l = (0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b) / 255.0;
                    sum += l;
                    sumSq += l * l;
                }
                double mean = sum / px.Length;
                double var0 = System.Math.Max(0.0, sumSq / px.Length - mean * mean);
                return (float)System.Math.Sqrt(var0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SHIFTIT] FrameContrast failed: " + e.Message);
                return -1f;
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (tex != null) Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// Grab one frame at the given resolution. Returns the full path written, or null on
        /// failure - and a failure is LOUD, because a silently missing screenshot would turn a
        /// visual check into a check that did not run.
        /// </summary>
        public static string Shot(Camera cam, string name, int width = 1920, int height = 1080)
        {
            if (cam == null)
            {
                Debug.LogError("[SHIFTIT] CAPTURE FAILED (" + name + "): no camera.");
                return null;
            }

            RenderTexture rt = null;
            Texture2D tex = null;
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                { antiAliasing = 1, name = "capture" };
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply(false, false);

                var bytes = tex.EncodeToPNG();
                if (bytes == null || bytes.Length == 0)
                {
                    Debug.LogError("[SHIFTIT] CAPTURE FAILED (" + name + "): encoded to 0 bytes.");
                    return null;
                }

                string path = Path.Combine(SessionDir, name + ".png");
                File.WriteAllBytes(path, bytes);
                Debug.Log("[SHIFTIT] shot " + name + " -> " + path + " (" + bytes.Length / 1024 + " KB)");
                return path;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SHIFTIT] CAPTURE FAILED (" + name + "): " + e.Message);
                return null;
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (tex != null) Object.DestroyImmediate(tex);
            }
        }
    }
}
