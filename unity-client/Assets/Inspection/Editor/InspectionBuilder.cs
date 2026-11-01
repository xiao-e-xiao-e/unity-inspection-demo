using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Inspection.Editor
{
    public static class InspectionBuilder
    {
        [MenuItem("Inspection/Prepare Inspection Scene")]
        public static void PrepareScene()
        {
            EditorSceneManager.OpenScene(InspectionSceneBuilder.ScenePath);
            var motor=UnityEngine.Object.FindObjectOfType<FirstPersonMotor>();
            var probe=UnityEngine.Object.FindObjectOfType<SurfaceProbe>();
            var hud=UnityEngine.Object.FindObjectOfType<InspectionHud>();
            var api=motor.GetComponent<IssueApiClient>();if(api==null)api=motor.gameObject.AddComponent<IssueApiClient>();
            var flow=motor.GetComponent<IssueWorkflow>();if(flow==null)flow=motor.gameObject.AddComponent<IssueWorkflow>();
            flow.api=api;flow.probe=probe;flow.motor=motor;probe.workflow=flow;hud.workflow=flow;
            PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed; // Local demo API uses HTTP.
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(InspectionSceneBuilder.ScenePath);
            InspectionSceneBuilder.Validate();
            if(UnityEngine.Object.FindObjectOfType<InspectionHud>().workflow==null)throw new Exception("Workflow reference not saved");
            Debug.Log("INSPECTION_SCENE_PASS");
        }
        [MenuItem("Inspection/Build Windows Client")]
        public static void BuildAll()
        {
            PrepareScene();Directory.CreateDirectory("../bin/unity");
            var report=BuildPipeline.BuildPlayer(new[]{InspectionSceneBuilder.ScenePath},"../bin/unity/InspectionDemo.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Inspection build failed: "+report.summary.result);
            Debug.Log("INSPECTION_BUILD_PASS");
        }
    }
}
