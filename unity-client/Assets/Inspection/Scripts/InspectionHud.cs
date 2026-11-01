using UnityEngine;

namespace Inspection
{
    // Inspection form and status HUD. UI hit regions explicitly block scene clicks.
    public sealed class InspectionHud : MonoBehaviour
    {
        public FirstPersonMotor motor;
        public SurfaceProbe probe;
        public IssueWorkflow workflow;
        private bool editingBackend;
        private bool previousDraft;
        private int suppressedFrame=-1;
        private string addressDraft;
        public bool BlocksSceneInput => (workflow!=null && workflow.DraftOpen) || editingBackend || suppressedFrame==Time.frameCount;
        private GUIStyle heading, text, muted, coordinate, button, panel;
        private GUIStyle field, feedback;
        private Font font;
        private float Scale => Mathf.Clamp(Screen.width / 1440f, 0.65f, 1.3f);
        private Rect Header => new Rect(24, 24, 390, 246);
        private Rect Footer => new Rect(24, Screen.height / Scale - 226, 520, 202);

        public bool ContainsScreenPoint(Vector2 point)
        {
            Vector2 gui = new Vector2(point.x, Screen.height - point.y) / Scale;
            return BlocksSceneInput || Header.Contains(gui) || Footer.Contains(gui);
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
            field = new GUIStyle(GUI.skin.textField) { font=font, fontSize=18, padding=new RectOffset(10,10,8,8) };
            feedback = new GUIStyle(text) { wordWrap=true };
        }

        private void OnGUI()
        {
            PrepareStyles();
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Scale);
            if(workflow!=null) DrawInspection();
            else GUI.Label(new Rect(42,35,800,50),"巡检组件配置缺失，请重新打开 InspectionRoom 场景。",heading);
            GUI.matrix = previous;
        }
        private void DrawInspection()
        {
            bool modal=workflow.DraftOpen || editingBackend;
            GUI.enabled=!modal;
            DrawPanel(Header);
            GUI.Label(new Rect(42,35,350,36),"设备间 · 问题上报",heading);
            GUI.Label(new Rect(42,76,350,28),"WASD 移动  |  按住右键转动视角",text);
            GUI.Label(new Rect(42,108,350,28),"左键标注  |  Esc 取消未提交问题",muted);
            if(GUI.Button(new Rect(42,148,165,38),"回到起点 [R]",button))motor.ResetView();
            GUI.enabled=!modal && !workflow.CheckingHealth;
            if(GUI.Button(new Rect(220,148,165,38),workflow.CheckingHealth?"正在检查…":"检查后端",button))workflow.CheckHealth();
            GUI.enabled=!modal && !workflow.CheckingHealth;
            if(GUI.Button(new Rect(42,200,343,38),"后端地址设置",button))
            { editingBackend=true;addressDraft=workflow.api.baseUrl;motor.ReleasePointer();suppressedFrame=Time.frameCount; }
            GUI.enabled=true;
            DrawPanel(Footer);
            GUI.Label(new Rect(Footer.x+18,Footer.y+10,484,28),"本次已上报 "+workflow.SavedCount+" 条",text);
            feedback.normal.textColor=workflow.IsError?new Color(1f,0.53f,0.47f):new Color(0.7f,0.9f,0.82f);
            GUI.Label(new Rect(Footer.x+18,Footer.y+47,484,115),workflow.Feedback,feedback);
            GUI.Label(new Rect(Footer.x+18,Footer.y+165,484,26),"黄色：未保存    绿色：后端已确认保存",muted);
            if(workflow.DraftOpen)DrawForm();
            else if(editingBackend)DrawAddress();
            if(previousDraft && !workflow.DraftOpen) {GUI.FocusControl(null);suppressedFrame=Time.frameCount;}
            previousDraft=workflow.DraftOpen;
        }

        private void ModalBackground(Rect box)
        {
            Color before=GUI.color;GUI.color=new Color(0,0,0,0.65f);
            GUI.DrawTexture(new Rect(0,0,Screen.width/Scale,Screen.height/Scale),Texture2D.whiteTexture);
            GUI.color=new Color(0.08f,0.13f,0.19f,1f);GUI.DrawTexture(box,Texture2D.whiteTexture);GUI.color=before;
        }

        private void DrawForm()
        {
            Rect box=new Rect((Screen.width/Scale-600)/2,(Screen.height/Scale-620)/2,600,620);
            ModalBackground(box);GUI.BeginGroup(box);
            GUI.Label(new Rect(28,18,544,38),"上报巡检问题",heading);
            GUI.Label(new Rect(28,63,544,30),probe.TargetName+"  /  世界坐标",muted);
            var p=probe.HitPosition;
            GUI.Label(new Rect(28,95,544,28),string.Format("X {0:F3}    Y {1:F3}    Z {2:F3}",p.x,p.y,p.z),coordinate);
            GUI.enabled=!workflow.IsSubmitting;
            GUI.Label(new Rect(28,134,544,28),"标题 *（最多 120 字符）",text);
            GUI.SetNextControlName("IssueTitle");workflow.Title=GUI.TextField(new Rect(28,165,544,42),workflow.Title,240,field);
            GUI.Label(new Rect(28,219,544,28),"描述（可选，最多 2000 字符）",text);
            var area=new GUIStyle(field){wordWrap=true};
            workflow.Description=GUI.TextArea(new Rect(28,250,544,112),workflow.Description,4000,area);
            GUI.Label(new Rect(28,374,544,28),"优先级",text);
            workflow.PriorityIndex=GUI.SelectionGrid(new Rect(28,405,544,40),workflow.PriorityIndex,new[]{"低 / low","中 / medium","高 / high"},3,button);
            GUI.enabled=true;
            GUI.Label(new Rect(28,457,544,88),workflow.Feedback,feedback);
            GUI.enabled=!workflow.IsSubmitting && !workflow.CheckingHealth;
            if(GUI.Button(new Rect(28,560,260,42),workflow.IsSubmitting?"正在提交…":"提交问题",button))workflow.Submit();
            GUI.enabled=!workflow.IsSubmitting;
            if(GUI.Button(new Rect(304,560,268,42),"取消并移除标记",button)){workflow.Cancel();suppressedFrame=Time.frameCount;}
            GUI.enabled=true;
            if(!previousDraft && Event.current.type==EventType.Repaint)GUI.FocusControl("IssueTitle");
            GUI.EndGroup();
        }

        private void DrawAddress()
        {
            Rect box=new Rect((Screen.width/Scale-600)/2,(Screen.height/Scale-250)/2,600,250);
            ModalBackground(box);GUI.BeginGroup(box);
            GUI.Label(new Rect(28,18,544,38),"后端地址",heading);
            GUI.Label(new Rect(28,65,544,30),"默认 http://127.0.0.1:8080；仅对本次运行生效",muted);
            addressDraft=GUI.TextField(new Rect(28,105,544,42),addressDraft,512,field);
            if(GUI.Button(new Rect(28,180,260,42),"应用并检查",button))
            {workflow.api.baseUrl=addressDraft.Trim();editingBackend=false;GUI.FocusControl(null);suppressedFrame=Time.frameCount;workflow.CheckHealth();}
            if(GUI.Button(new Rect(304,180,268,42),"关闭",button))
            {editingBackend=false;GUI.FocusControl(null);suppressedFrame=Time.frameCount;}
            GUI.EndGroup();
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
