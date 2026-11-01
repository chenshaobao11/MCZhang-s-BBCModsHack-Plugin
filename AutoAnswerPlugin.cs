using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UniversalHack
{
    public class AutoAnswerPlugin : MonoBehaviour
    {
        private static Type         mathGameType;
        private static FieldInfo    solutionField;
        private static FieldInfo    problemField;
        private static FieldInfo    questionInProgressField;
        private static FieldInfo    playerAnswerField;
        private static MethodInfo   checkAnswerMethod;
        private static PropertyInfo textProperty;   // TMP_InputField.text

        // 记录每个实例处理过的题号，避免重复提交
        private readonly Dictionary<int, int> lastProblemByInstance = new Dictionary<int, int>();

        private float lastScanTime;
        private const float ScanInterval = 0.1f;

        private void Awake()
        {
            try
            {
                mathGameType = GameReflection.FindType("MathGameScript");
                if (mathGameType == null)
                {
                    ModLog.ReportError("AutoAnswerPlugin", "Awake");
                    return;
                }

                solutionField           = GameReflection.Field(mathGameType, "solution");
                problemField            = GameReflection.Field(mathGameType, "problem");
                questionInProgressField = GameReflection.Field(mathGameType, "questionInProgress");
                playerAnswerField       = GameReflection.Field(mathGameType, "playerAnswer");
                checkAnswerMethod       = GameReflection.Method(mathGameType, "CheckAnswer");

                if (solutionField == null || problemField == null ||
                    questionInProgressField == null || playerAnswerField == null ||
                    checkAnswerMethod == null)
                {
                    ModLog.ReportError("AutoAnswerPlugin", "Awake");
                }
            }
            catch (Exception ex) { ModLog.Error("AutoAnswerPlugin.Awake", ex); }
        }

        private void Update()
        {
            try
            {
                if (!HackMenu.ActiveFeatures.Contains(Features.AutoAnswer)) return;
                if (mathGameType == null || solutionField == null || problemField == null ||
                    questionInProgressField == null || playerAnswerField == null ||
                    checkAnswerMethod == null) return;

                if (Time.unscaledTime - this.lastScanTime < ScanInterval) return;
                this.lastScanTime = Time.unscaledTime;

                var instances = UnityEngine.Object.FindObjectsOfType(mathGameType);
                if (instances == null || instances.Length == 0) return;

                for (int i = 0; i < instances.Length; i++)
                {
                    var inst = instances[i];
                    if (inst == null) continue;

                    int id = inst.GetInstanceID();

                    // 读取当前题号，problem > 3 表示题目已结束，不再处理
                    int problem;
                    try { problem = Convert.ToInt32(problemField.GetValue(inst)); }
                    catch { continue; }
                    if (problem > 3) continue;

                    // 这一题已经处理过就跳过
                    if (this.lastProblemByInstance.TryGetValue(id, out var prev) && prev == problem)
                        continue;

                    // 必须处于等待输入状态
                    bool qip;
                    try { qip = (bool)questionInProgressField.GetValue(inst); }
                    catch { continue; }
                    if (!qip) continue;

                    // 读正确答案
                    float solution;
                    try { solution = Convert.ToSingle(solutionField.GetValue(inst)); }
                    catch { continue; }
                    string answerText = Mathf.RoundToInt(solution).ToString();

                    // 写入 playerAnswer.text（TMP_InputField 通过反射避免额外引用 TMPro）
                    object inputField = playerAnswerField.GetValue(inst);
                    if (inputField == null) continue;
                    try
                    {
                        if (textProperty == null)
                            textProperty = inputField.GetType().GetProperty("text");
                        if (textProperty == null) continue;
                        textProperty.SetValue(inputField, answerText);
                    }
                    catch { continue; }

                    // 记录已处理，再调用 CheckAnswer 提交
                    this.lastProblemByInstance[id] = problem;

                    try { checkAnswerMethod.Invoke(inst, null); }
                    catch (Exception ex) { ModLog.Error("AutoAnswerPlugin.Update", ex); }
                }
            }
            catch (Exception ex) { ModLog.Error("AutoAnswerPlugin.Update", ex); }
        }
    }
}