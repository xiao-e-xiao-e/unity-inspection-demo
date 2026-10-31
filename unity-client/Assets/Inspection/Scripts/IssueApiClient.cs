using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Inspection
{
    [Serializable] public sealed class IssuePosition { public float x, y, z; public IssuePosition(Vector3 v) { x=v.x; y=v.y; z=v.z; } }
    [Serializable] public sealed class CreateIssueRequest { public string title, description, priority; public IssuePosition position; }
    [Serializable] public sealed class IssueRecord { public string id, title, description, priority, status, createdAt, updatedAt; public IssuePosition position; }
    [Serializable] public sealed class IssueList { public IssueRecord[] items; }
    [Serializable] public sealed class ApiErrorDetail { public string code, message, field; }
    [Serializable] public sealed class ApiErrorResponse { public ApiErrorDetail error; }
    [Serializable] public sealed class HealthResponse { public string status; }

    public sealed class IssueApiClient : MonoBehaviour
    {
        public string baseUrl = "http://127.0.0.1:8080";
        public int timeoutSeconds = 8;
        private UnityWebRequest active;

        private void Awake()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-apiBaseUrl");
            if(index>=0 && index+1<args.Length) baseUrl=args[index+1];
        }
        public bool ValidAddress(out string error)
        {
            error="";
            if(!Uri.TryCreate(baseUrl.Trim(),UriKind.Absolute,out var uri) ||
               (uri.Scheme!="http" && uri.Scheme!="https") || !string.IsNullOrEmpty(uri.UserInfo) ||
               !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            { error="后端地址必须是有效的 http/https 地址，且不能包含账号、查询参数或片段。"; return false; }
            return true;
        }
        public IEnumerator Create(CreateIssueRequest body, Action<IssueRecord,string> complete)
        {
            if(!ValidAddress(out var error)){complete(null,error);yield break;}
            using(var request=new UnityWebRequest(baseUrl.Trim().TrimEnd('/')+"/api/issues","POST"))
            {
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json; charset=utf-8");
                request.timeout=timeoutSeconds;request.redirectLimit=0;active=request;
                yield return request.SendWebRequest();active=null;
                if(request.result!=UnityWebRequest.Result.Success || request.responseCode!=201)
                {complete(null,DescribeFailure(request,true));yield break;}
                IssueRecord issue=null;
                try { issue=JsonUtility.FromJson<IssueRecord>(request.downloadHandler.text); }
                catch(ArgumentException) { }
                if(issue==null || string.IsNullOrEmpty(issue.id) || issue.status!="open" || issue.position==null ||
                   string.IsNullOrEmpty(issue.createdAt) || string.IsNullOrEmpty(issue.updatedAt) ||
                   issue.title!=body.title || issue.priority!=body.priority ||
                   issue.position.x!=body.position.x || issue.position.y!=body.position.y || issue.position.z!=body.position.z)
                {complete(null,"后端返回内容不完整或与提交不一致，结果未确认。请先检查网页列表，再决定是否重试。");yield break;}
                complete(issue,null);
            }
        }
        public IEnumerator Health(Action<string> complete)
        {
            if(!ValidAddress(out var error)){complete(error);yield break;}
            using(var request=UnityWebRequest.Get(baseUrl.Trim().TrimEnd('/')+"/api/health"))
            {
                request.timeout=timeoutSeconds;request.redirectLimit=0;active=request;
                yield return request.SendWebRequest();active=null;
                if(request.result!=UnityWebRequest.Result.Success){complete(DescribeFailure(request,false));yield break;}
                HealthResponse health=null;try{health=JsonUtility.FromJson<HealthResponse>(request.downloadHandler.text);}catch(ArgumentException){}
                complete(health!=null && health.status=="ok" ? null : "健康检查返回内容无效。");
            }
        }
        private static string DescribeFailure(UnityWebRequest request,bool creating)
        {
            if(request.result==UnityWebRequest.Result.ConnectionError)
                return creating ? "无法连接后端或请求超时，结果未确认。输入已保留；请检查后端并先核对网页列表，再决定是否重试。" : "无法连接后端，请检查地址和 Go 服务是否启动。";
            string detail="请求失败";
            try { var response=JsonUtility.FromJson<ApiErrorResponse>(request.downloadHandler.text); if(!string.IsNullOrEmpty(response?.error?.message))detail=response.error.message; }catch(ArgumentException){}
            return "HTTP "+request.responseCode+"："+detail+(creating && request.responseCode>=500 ? "。结果未确认，请先核对网页列表。" : "");
        }
        private void OnDestroy() { if(active!=null){active.Abort();active.Dispose();active=null;} }
    }
}
