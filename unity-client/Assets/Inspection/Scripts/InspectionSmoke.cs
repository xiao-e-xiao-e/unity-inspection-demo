#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Inspection
{
    public sealed class InspectionSmoke : MonoBehaviour
    {
        private bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-inspectionSmoke")<0)return;
            Application.runInBackground=true;new GameObject("Inspection integration smoke").AddComponent<InspectionSmoke>();
        }
        private void OnEnable(){Application.logMessageReceived+=OnLog;}
        private void OnDisable(){Application.logMessageReceived-=OnLog;}
        private void OnLog(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)failed=true;}
        private void Require(bool value,string message){if(!value){failed=true;Debug.LogError(message);}}
        private IEnumerator AwaitSubmission(IssueWorkflow flow)
        {
            float end=Time.realtimeSinceStartup+12;
            while(flow.IsSubmitting && Time.realtimeSinceStartup<end)yield return null;
            Require(!flow.IsSubmitting,"Submission did not finish");
        }
        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(6);
            var flow=FindObjectOfType<IssueWorkflow>();var probe=flow.probe;var hud=FindObjectOfType<InspectionHud>();
            string live=flow.api.baseUrl;
            Require(probe.TryPick(new Ray(new Vector3(3,1.4f,-3),Vector3.forward),out var hit),"No cabinet hit");
            probe.ShowHit(hit);Require(flow.DraftOpen && hud.BlocksSceneInput,"Draft must block movement/scene input");
            int clickCount=probe.HitCount;probe.ShowHit(hit);Require(probe.HitCount==clickCount,"Open form accepted another selection");
            flow.Title="   ";flow.Submit();Require(flow.IsError && flow.CreateAttempts==0,"Blank title sent request");
            flow.Title="测试";flow.Description=new string('测',2001);flow.Submit();Require(flow.CreateAttempts==0,"Long description sent request");
            flow.Cancel();yield return null;
            Require(!flow.DraftOpen && !probe.HasHit && GameObject.Find("Raycast hit preview (not saved)")==null,"Cancel left pending marker");
            probe.ShowHit(hit);flow.Title="巡检联调测试 🔧";flow.Description="机柜右侧温度偏高\n中文与换行验证";flow.PriorityIndex=0;
            flow.Submit();flow.Submit();flow.Cancel();
            Require(flow.IsSubmitting && flow.DraftOpen && flow.CreateAttempts==1,"Double submit/cancel while submitting was accepted");
            yield return AwaitSubmission(flow);
            Require(!flow.DraftOpen && flow.SavedCount==1 && flow.LastSaved!=null && flow.LastSaved.priority=="low","Valid creation failed: "+flow.Feedback);
            Require(!probe.HasHit && GameObject.Find("Raycast hit preview (not saved)")==null,"Saved marker remained pending");
            if(flow.LastSaved!=null)Require(GameObject.Find("Saved issue / "+flow.LastSaved.id)!=null,"Saved marker missing");
            probe.ShowHit(hit);flow.Title="网络恢复后重试";flow.Description="输入应该保留";flow.PriorityIndex=2;
            flow.api.baseUrl="not-a-url";flow.Submit();Require(flow.IsError && flow.CreateAttempts==1,"Invalid URL sent a request");
            flow.api.baseUrl="http://127.0.0.1:18085";flow.Submit();yield return AwaitSubmission(flow);
            Require(flow.IsError && flow.DraftOpen && probe.HasHit && flow.Title=="网络恢复后重试" && flow.SavedCount==1,"Network failure lost draft or reported success");
            flow.api.baseUrl=live;flow.Submit();yield return AwaitSubmission(flow);
            Require(!flow.DraftOpen && flow.SavedCount==2,"Retry after recovery failed");

            var body=new CreateIssueRequest{title="错误响应测试",description="",priority="urgent",position=new IssuePosition(hit.point)};
            string failure=null;
            yield return flow.api.Create(body,(issue,error)=>failure=error);
            Require(failure!=null && failure.Contains("HTTP 400"),"Backend validation error not surfaced");
            body.priority="medium";
            foreach(var route in new[]{"malformed","error500","slow"})
            {
                flow.api.baseUrl="http://127.0.0.1:18084/"+route;flow.api.timeoutSeconds=1;failure=null;
                yield return flow.api.Create(body,(issue,error)=>failure=error);
                Require(!string.IsNullOrEmpty(failure),"Fault endpoint was accepted: "+route);
            }
            flow.api.baseUrl=live;flow.api.timeoutSeconds=8;
            using(var get=UnityWebRequest.Get(live+"/api/issues"))
            {
                get.timeout=8;yield return get.SendWebRequest();
                Require(get.result==UnityWebRequest.Result.Success,"List unavailable");
                var list=JsonUtility.FromJson<IssueList>(get.downloadHandler.text);
                Require(list.items.Length==2,"Duplicate or invalid issue persisted");
            }
            probe.ShowHit(hit);flow.Title="2 号机柜温度异常";flow.Description="机柜右侧温度明显偏高，需要检查散热情况。";flow.PriorityIndex=2;
            yield return new WaitForEndOfFrame();
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-smokeScreenshot");
            if(index>=0 && index+1<args.Length)
            {
                ScreenCapture.CaptureScreenshot(args[index+1]);for(int i=0;i<30;i++)yield return null;
                Require(File.Exists(args[index+1]),"Screenshot missing");
                if(File.Exists(args[index+1]))
                {
                    var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(args[index+1]));int lit=0;
                    foreach(var pixel in image.GetPixels32())if(pixel.r+pixel.g+pixel.b>45)lit++;
                    Require(lit>image.width*image.height/10,"Screenshot blank");Destroy(image);
                }
            }
            flow.Cancel();
            Debug.Log(failed?"INSPECTION_RUNTIME_FAIL":"INSPECTION_RUNTIME_PASS: draft cancellation, validation, duplicate suppression, actual Go save, retry, HTTP 400/500, malformed response, timeout, rendered form.");
            Application.Quit(failed?1:0);
        }
    }
}
#endif
