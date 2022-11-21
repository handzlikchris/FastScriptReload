using System;
using UnityEngine;

namespace AssetStoreTools.Validator
{
    internal class AutomatedTest : ValidationTest
    {
        public AutomatedTest(ValidationTestScriptableObject source) : base(source) { }

        public override void Run()
        {
            var actionsObject = TestActions.Instance;
            var method = actionsObject.GetType().GetMethod(TestMethodName);
            if (method != null)
            {
                try
                {
                    Result = (TestResult)method.Invoke(actionsObject, null);
                }
                catch (Exception e)
                {
                    var result = new TestResult() { Result = TestResult.ResultStatus.Fail };
                    result.AddMessage("An exception was caught when running this test case. See Console for more details");
                    Debug.LogError($"An exception was caught when running validation for test case '{Title}'\n{e.InnerException}");
                    Result = result;
                }
                finally
                {
                    OnTestCompleted();
                }
            }
            else
            {
                Debug.LogError("Cannot invoke method \"" + TestMethodName + "\". No such method found");
            }
        }
    }
}