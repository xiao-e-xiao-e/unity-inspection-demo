using UnityEngine;

namespace Inspection
{
    // Stage-two diagnostic HUD. UI hit regions explicitly block scene clicks.
    public sealed class InspectionHud : MonoBehaviour
    {
        public FirstPersonMotor motor;
        public SurfaceProbe probe;
        private GUIStyle heading, text, muted, coordinate, button, panel;
        private Font font;
        private float Scale => Mathf.Clamp(Screen.width / 1440f, 0.65f, 1.3f);
        private Rect Header => new Rect(24, 24, 390, 184);
        private Rect Footer => new Rect(24, Screen.height / Scale - 180, 490, 156);

        public bool ContainsScreenPoint(Vector2 point)
        {
            Vector2 gui = new Vector2(point.x, Screen.height - point.y) / Scale;
            return Header.Contains(gui) || Footer.Contains(gui);
        }

        private void PrepareStyles()
        {
            if (heading != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 18);
            heading = new GUIStyle(GUI.skin.label) { font = font, fontSize = 24, fontStyle = FontStyle.Bold };
            text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17 };
            muted = new GUIStyle(text); muted.normal.textColor = new Color(0.66f, 0.78f, 0.87f);
            coordinate = new GUIStyle(text); coordinate.normal.textColor = new Color(1f, 0.77f, 0.3f);
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16 };
            panel = new GUIStyle(GUI.skin.box);
        }

        private void OnGUI()
        {
            PrepareStyles();
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Scale);
            DrawPanel(Header);
            GUI.Label(new Rect(42, 35, 350, 36), "设备间 · 虚拟巡检", heading);
            GUI.Label(new Rect(42, 75, 350, 28), "WASD 移动   |   按住右键转动视角", text);
            GUI.Label(new Rect(42, 106, 350, 28), "左键检测表面   |   Esc 清除命中点", muted);
            if (GUI.Button(new Rect(42, 150, 165, 38), "回到起点 [R]", button)) motor.ResetView();
            if (GUI.Button(new Rect(220, 150, 165, 38), "清除命中点", button)) probe.Clear();
            Rect bottom = Footer;
            DrawPanel(bottom);
            GUI.Label(new Rect(bottom.x + 18, bottom.y + 12, 460, 28), "射线命中 / " + probe.TargetName, text);
            string position = probe.HasHit ? string.Format("X {0:F3}    Y {1:F3}    Z {2:F3}", probe.HitPosition.x, probe.HitPosition.y, probe.HitPosition.z) : "点击机柜、管道、地面或墙面进行验证";
            GUI.Label(new Rect(bottom.x + 18, bottom.y + 47, 460, 28), position, coordinate);
            GUI.Label(new Rect(bottom.x + 18, bottom.y + 84, 460, 26), "第二阶段：命中预览，未保存或上报", muted);
            GUI.Label(new Rect(bottom.x + 18, bottom.y + 113, 460, 26), "有效点击次数：" + probe.HitCount, muted);
            GUI.matrix = previous;
        }

        private void DrawPanel(Rect rect)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.06f, 0.12f, 0.18f, 0.95f);
            GUI.Box(rect, GUIContent.none, panel);
            GUI.backgroundColor = previous;
        }

        private void OnDestroy() { if (font != null) Destroy(font); }
    }
}
