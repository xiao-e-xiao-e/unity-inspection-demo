using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Inspection.Editor
{
    public static class InspectionSceneBuilder
    {
        private const string Root = "Assets/Inspection";
        public const string ScenePath = Root + "/Scenes/InspectionRoom.unity";
        private static Material floor, wall, dark, steel, cyan, green, amber;

        [MenuItem("Inspection/Rebuild Inspection Scene")]
        public static void BuildScene()
        {
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            floor = Material("Floor", new Color(0.22f, 0.28f, 0.34f));
            wall = Material("Wall", new Color(0.49f, 0.58f, 0.65f));
            dark = Material("Cabinet", new Color(0.08f, 0.14f, 0.2f));
            steel = Material("Steel", new Color(0.38f, 0.51f, 0.59f));
            cyan = Material("Blue", new Color(0.05f, 0.58f, 0.82f));
            green = Material("Indicator", new Color(0.2f, 0.9f, 0.56f), true);
            amber = Material("HitPreview", new Color(1f, 0.65f, 0.08f), true);

            Target("Floor", "地面", new Vector3(0,-0.15f,0), new Vector3(14,0.3f,18), floor);
            Target("Wall North", "北墙", new Vector3(0,2,9), new Vector3(14,4,0.3f), wall);
            Target("Wall South", "南墙", new Vector3(0,2,-9), new Vector3(14,4,0.3f), wall);
            Target("Wall East", "东墙", new Vector3(7,2,0), new Vector3(0.3f,4,18), wall);
            Target("Wall West", "西墙", new Vector3(-7,2,0), new Vector3(0.3f,4,18), wall);
            Target("Ceiling", "顶棚", new Vector3(0,4.1f,0), new Vector3(14,0.2f,18), floor);
            for (int z=-8;z<=8;z+=2) Decoration("Floor seam", new Vector3(0,0.003f,z), new Vector3(13.6f,0.008f,0.025f), steel);
            for (int x=-6;x<=6;x+=2) Decoration("Floor seam", new Vector3(x,0.003f,0), new Vector3(0.025f,0.008f,17.6f), steel);
            Decoration("Aisle stripe L",new Vector3(-1.3f,0.009f,0),new Vector3(0.06f,0.01f,16),cyan);
            Decoration("Aisle stripe R",new Vector3(1.3f,0.009f,0),new Vector3(0.06f,0.01f,16),cyan);

            Cabinet("CAB-01", "1 号机柜", new Vector3(-3,1.35f,1.7f));
            Cabinet("CAB-02", "2 号机柜", new Vector3(3,1.35f,1.7f));
            var pump = Target("PUMP-01", "循环水泵", new Vector3(-3,0.5f,6),new Vector3(1.8f,1,1.5f),cyan);
            Decoration("Pump plinth",new Vector3(-3,0.12f,6),new Vector3(2.5f,0.24f,2.1f),dark);
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = "PIPE-01"; pipe.layer=8; pipe.transform.position=new Vector3(3,1.6f,6);
            pipe.transform.localScale=new Vector3(0.5f,1.6f,0.5f);pipe.GetComponent<Renderer>().sharedMaterial=steel;
            var target=pipe.AddComponent<InspectionTarget>();target.objectId="PIPE-01";target.displayName="冷却管道";
            var horizontal=Target("PIPE-02","冷却管道横段",new Vector3(0,3.1f,6),new Vector3(6,0.3f,0.3f),steel);
            Label("PUMP / 01",new Vector3(-3,1.28f,5.2f),0.11f);
            Label("COOLING / 02",new Vector3(3,2.6f,5.7f),0.09f);
            Label("INSPECTION LAB",new Vector3(0,3.25f,8.79f),0.18f);
            Label("EQUIPMENT ROOM   /   ZONE A",new Vector3(0,2.8f,8.78f),0.08f);
            for(int z=-5;z<=7;z+=6)
            {
                Decoration("Light housing",new Vector3(0,3.9f,z),new Vector3(5,0.08f,0.35f),steel);
                var lamp=new GameObject("Ceiling light").AddComponent<Light>();
                lamp.type=LightType.Point;lamp.range=11;lamp.intensity=1.4f;lamp.color=new Color(0.77f,0.87f,1f);lamp.transform.position=new Vector3(0,3.65f,z);
            }
            var sun=new GameObject("Soft directional light").AddComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=0.85f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(50,-25,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(0.46f,0.53f,0.63f);
            RenderSettings.skybox=null;

            var player=new GameObject("Inspector");player.transform.position=new Vector3(0,0.05f,-6.5f);
            var controller=player.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=0.3f;controller.center=new Vector3(0,0.9f,0);controller.stepOffset=0.25f;
            var cameraObject=new GameObject("Inspection Camera");cameraObject.tag="MainCamera";cameraObject.transform.SetParent(player.transform,false);cameraObject.transform.localPosition=new Vector3(0,1.65f,0);
            var camera=cameraObject.AddComponent<Camera>();camera.fieldOfView=65;camera.nearClipPlane=0.05f;camera.farClipPlane=60;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.12f,0.18f,0.25f);
            cameraObject.AddComponent<AudioListener>();
            var motor=player.AddComponent<FirstPersonMotor>();motor.viewCamera=camera;
            var probe=player.AddComponent<SurfaceProbe>();probe.viewCamera=camera;probe.markerMaterial=amber;
            var hud=new GameObject("Inspection HUD").AddComponent<InspectionHud>();hud.motor=motor;hud.probe=probe;motor.hud=hud;probe.hud=hud;
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue="InspectionSurface";tags.ApplyModifiedProperties();
            PlayerSettings.productName="Virtual Inspection";PlayerSettings.companyName="Inspection Demo";
            PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=false;
            QualitySettings.vSyncCount=1;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            Validate();
            InspectionBuilder.PrepareScene();
            Debug.Log("SCENE_SCENE_PASS: scene generated, saved and validated.");
        }

        private static Material Material(string name,Color color,bool emission=false)
        {
            string path=Root+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
            material.color=color;material.SetFloat("_Glossiness",0.3f);
            if(emission){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*0.7f);}
            EditorUtility.SetDirty(material);return material;
        }

        private static GameObject Box(string name,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.position=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;return obj;
        }
        private static GameObject Target(string id,string name,Vector3 position,Vector3 scale,Material material)
        {
            var obj=Box(id,position,scale,material);obj.layer=8;var target=obj.AddComponent<InspectionTarget>();target.objectId=id;target.displayName=name;return obj;
        }
        private static void Decoration(string name,Vector3 position,Vector3 scale,Material material)
        {
            var obj=Box(name,position,scale,material);obj.layer=2;Object.DestroyImmediate(obj.GetComponent<Collider>());
        }
        private static void Cabinet(string id,string name,Vector3 position)
        {
            Target(id,name,position,new Vector3(1.8f,2.7f,1.4f),dark);
            Decoration(id+" door",position+new Vector3(0,0,-0.711f),new Vector3(1.62f,2.4f,0.025f),steel);
            Decoration(id+" display",position+new Vector3(0,0.55f,-0.74f),new Vector3(1.1f,0.55f,0.035f),cyan);
            Decoration(id+" status",position+new Vector3(0.6f,0.95f,-0.76f),new Vector3(0.1f,0.1f,0.04f),green);
            for(int i=0;i<5;i++) Decoration("Vent",position+new Vector3(0,-0.35f-i*0.14f,-0.75f),new Vector3(1.15f,0.04f,0.035f),dark);
            Label(id,position+new Vector3(0,1.04f,-0.8f),0.075f);
        }
        private static void Label(string value,Vector3 position,float size)
        {
            var obj=new GameObject("Label: " + value);obj.transform.position=position;var text=obj.AddComponent<TextMesh>();text.text=value;text.fontSize=64;text.characterSize=size*0.4f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(0.8f,0.93f,1f);
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");obj.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
        }

        [MenuItem("Inspection/Validate Scene")]
        public static void Validate()
        {
            Physics.SyncTransforms();
            var probe=Object.FindObjectOfType<SurfaceProbe>();
            if(probe==null)throw new Exception("Missing SurfaceProbe");
            foreach(var id in new[]{"CAB-01","CAB-02","PUMP-01","PIPE-01","Floor","Wall North"})
            {
                var obj=GameObject.Find(id);if(obj==null||obj.GetComponent<Collider>()==null||obj.layer!=8)throw new Exception("Missing collider/target: "+id);
            }
            foreach(var id in new[]{"CAB-01","CAB-02"})
            {
                var cabinet=GameObject.Find(id);var origin=new Vector3(cabinet.transform.position.x,1.4f,-3);
                if(!probe.TryPick(new Ray(origin,Vector3.forward),out var hit)||hit.collider.gameObject!=cabinet||Mathf.Abs(hit.point.z-1f)>0.001f)throw new Exception("Raycast position mismatch: "+id);
            }
            if(!probe.TryPick(new Ray(new Vector3(0,2,-3),Vector3.down),out var floorHit)||Mathf.Abs(floorHit.point.y)>0.001f)throw new Exception("Floor ray mismatch");
            if(probe.TryPick(new Ray(new Vector3(0,5,0),Vector3.up),out _))throw new Exception("Empty sky should not hit");
            var motor=Object.FindObjectOfType<FirstPersonMotor>();var cc=motor.GetComponent<CharacterController>();var start=motor.transform.position;
            cc.Move(Vector3.forward*30);if(motor.transform.position.z>8.7f)throw new Exception("Character passed through wall");
            cc.enabled=false;motor.transform.position=start;cc.enabled=true;
            Debug.Log("SCENE_VALIDATION_PASS: cabinet/floor ray positions, no-hit and wall collision.");
        }

    }
}
