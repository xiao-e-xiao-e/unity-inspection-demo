using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Inspection.Editor
{
    public static class StageThreeBuilder
    {
        [MenuItem("Inspection/Upgrade Scene to Stage 3")]
        public static void Upgrade()
        {
            EditorSceneManager.OpenScene(StageTwoBuilder.ScenePath);
            var motor=UnityEngine.Object.FindObjectOfType<FirstPersonMotor>();
            var probe=UnityEngine.Object.FindObjectOfType<SurfaceProbe>();
            var hud=UnityEngine.Object.FindObjectOfType<InspectionHud>();
            var api=motor.GetComponent<IssueApiClient>();if(api==null)api=motor.gameObject.AddComponent<IssueApiClient>();
            var flow=motor.GetComponent<IssueWorkflow>();if(flow==null)flow=motor.gameObject.AddComponent<IssueWorkflow>();
            flow.api=api;flow.probe=probe;flow.motor=motor;probe.workflow=flow;hud.workflow=flow;
            PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed; // Local demo API uses HTTP.
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(StageTwoBuilder.ScenePath);
            StageTwoBuilder.Validate();
            if(UnityEngine.Object.FindObjectOfType<InspectionHud>().workflow==null)throw new Exception("Workflow reference not saved");
            Debug.Log("STAGE3_SCENE_PASS");
        }
        public static void BuildAll()
        {
            Upgrade();Directory.CreateDirectory("../bin/unity-stage3");
            var report=BuildPipeline.BuildPlayer(new[]{StageTwoBuilder.ScenePath},"../bin/unity-stage3/InspectionDemo.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Stage 3 build failed: "+report.summary.result);
            Debug.Log("STAGE3_BUILD_PASS");
        }
    }
}
