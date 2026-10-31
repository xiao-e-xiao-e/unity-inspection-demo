using System.Collections;
using UnityEngine;

namespace Inspection
{
    public sealed class IssueWorkflow : MonoBehaviour
    {
        public SurfaceProbe probe;
        public FirstPersonMotor motor;
        public IssueApiClient api;
        public bool DraftOpen { get; private set; }
        public bool IsSubmitting { get; private set; }
        public bool CheckingHealth { get; private set; }
        public string Title="", Description="";
        public int PriorityIndex=1;
        public string Feedback { get; private set; }="点击设备或场景表面，填写并上报问题。";
        public bool IsError { get; private set; }
        public IssueRecord LastSaved { get; private set; }
        public int SavedCount { get; private set; }
        public int CreateAttempts { get; private set; }
        public static readonly string[] Priorities={"low","medium","high"};

        public void BeginDraft()
        {
            if(DraftOpen || IsSubmitting)return;
            DraftOpen=true;Title="";Description="";PriorityIndex=1;
            Feedback="已记录命中位置。标题必填，描述可选。";IsError=false;
            motor.ReleasePointer();
        }
        public void Cancel()
        {
            if(IsSubmitting)return;
            if(!DraftOpen)return;
            DraftOpen=false;probe.Clear();Title="";Description="";
            Feedback="已取消，未保存标记已移除。";IsError=false;
        }
        public void Submit()
        {
            if(!DraftOpen || IsSubmitting || CheckingHealth)return;
            string title=Title.Trim();
            if(title.Length==0){SetError("请填写问题标题（不能只有空格）。");return;}
            if(Codepoints(title)>120){SetError("标题不能超过 120 个字符。");return;}
            if(Codepoints(Description)>2000){SetError("描述不能超过 2000 个字符。");return;}
            if(PriorityIndex<0||PriorityIndex>=Priorities.Length){SetError("请选择有效的优先级。");return;}
            if(!api.ValidAddress(out var error)){SetError(error);return;}
            IsSubmitting=true;IsError=false;Feedback="正在提交，请稍候…";CreateAttempts++;
            var body=new CreateIssueRequest{title=title,description=Description,priority=Priorities[PriorityIndex],position=new IssuePosition(probe.HitPosition)};
            StartCoroutine(SubmitRoutine(body));
        }
        private IEnumerator SubmitRoutine(CreateIssueRequest body)
        {
            yield return api.Create(body,(issue,error)=>
            {
                IsSubmitting=false;
                if(error!=null){SetError(error);return;}
                LastSaved=issue;SavedCount++;probe.Commit(issue);DraftOpen=false;
                Title="";Description="";IsError=false;
                Feedback="上报成功 · ID："+issue.id+"\n状态：open。可在网页刷新列表查看。";
            });
        }
        public void CheckHealth()
        {
            if(IsSubmitting||CheckingHealth)return;
            CheckingHealth=true;Feedback="正在检查服务…";IsError=false;StartCoroutine(HealthRoutine());
        }
        private IEnumerator HealthRoutine()
        {
            yield return api.Health(error=>{CheckingHealth=false;IsError=error!=null;Feedback=error??"Go 服务和数据库连接正常。";});
        }
        private void SetError(string error){Feedback=error;IsError=true;}
        private static int Codepoints(string value)
        {
            int count=0;for(int i=0;i<value.Length;i++,count++)if(char.IsHighSurrogate(value[i])&&i+1<value.Length&&char.IsLowSurrogate(value[i+1]))i++;
            return count;
        }
    }
}
