#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Inspection
{
    // Explicit opt-in verification in a development player; ordinary startup never runs this.
    public sealed class StageTwoSmoke : MonoBehaviour
    {
        private bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-stage2Smoke") >= 0)
            {
                Application.runInBackground = true;
                new GameObject("Stage2 smoke test").AddComponent<StageTwoSmoke>();
            }
        }
        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failed = true;
        }
        private IEnumerator Start()
        {
            // A development player may finish logic before the startup splash has rendered.
            yield return new WaitForSecondsRealtime(6f);
            yield return null;
            var probe = FindObjectOfType<SurfaceProbe>();
            var hud = FindObjectOfType<InspectionHud>();
            var motor = FindObjectOfType<FirstPersonMotor>();
            var cc = motor.GetComponent<CharacterController>();
            Require(hud.ContainsScreenPoint(new Vector2(40, Screen.height - 40)), "HUD must block pointer");
            Require(!hud.ContainsScreenPoint(new Vector2(Screen.width - 20, Screen.height * 0.5f)), "Scene area should allow pointer");
            Require(probe.TryPick(new Ray(new Vector3(3, 1.4f, -3), Vector3.forward), out var hit), "Cabinet ray failed");
            for (int i = 0; i < 10; i++) probe.ShowHit(hit);
            yield return null;
            int count = 0;
            foreach (var renderer in FindObjectsOfType<MeshRenderer>())
                if (renderer.name == "Raycast hit preview (not saved)") count++;
            Require(count == 1, "Repeated selections must reuse one preview");
            Require(Vector3.Distance(probe.HitPosition, hit.point) < 0.00001f, "Stored hit coordinate changed");
            probe.Clear();
            yield return null;
            Require(!probe.HasHit && GameObject.Find("Raycast hit preview (not saved)") == null, "Clear must remove preview");
            // Simulate travel in multiple frames instead of teleporting through a collider.
            for (int i = 0; i < 150; i++) { cc.Move(Vector3.forward * 0.15f); yield return null; }
            Require(motor.transform.position.z > 8f && motor.transform.position.z < 8.7f, "Movement or wall collision failed");
            motor.ResetView();
            yield return null;
            Require(Vector3.Distance(motor.transform.position, new Vector3(0, 0.05f, -6.5f)) < 0.15f, "Reset position failed");
            probe.ShowHit(hit);
            yield return new WaitForEndOfFrame();
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-smokeScreenshot");
            if (index >= 0 && index + 1 < args.Length)
            {
                ScreenCapture.CaptureScreenshot(args[index + 1]);
                for (int i = 0; i < 30; i++) yield return null;
                Require(File.Exists(args[index + 1]), "Screenshot was not saved");
                if (File.Exists(args[index + 1]))
                {
                    var image = new Texture2D(2, 2);
                    image.LoadImage(File.ReadAllBytes(args[index + 1]));
                    int lit = 0;
                    foreach (var pixel in image.GetPixels32()) if (pixel.r + pixel.g + pixel.b > 45) lit++;
                    Require(lit > image.width * image.height / 10, "Runtime screenshot is blank");
                    Destroy(image);
                }
            }
            Debug.Log(failed ? "STAGE2_RUNTIME_FAIL" : "STAGE2_RUNTIME_PASS: UI regions, preview lifecycle, hit coordinate, wall collision, reset, rendered HUD.");
            Application.Quit(failed ? 1 : 0);
        }
        private void Require(bool condition, string message) { if (!condition) { failed = true; Debug.LogError(message); } }
    }
}
#endif
