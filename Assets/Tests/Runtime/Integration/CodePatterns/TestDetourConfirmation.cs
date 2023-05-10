using System;
using System.Collections.Generic;

//no namespace on purpose, easier to access
public static class TestDetourConfirmation
{
    public static string MockRuntimeCodeChange = "//<mock-runtime-code-change>//";
    
    private static Dictionary<string, object> ConfirmationEntries { get; } = new Dictionary<string, object>();

    public static void Confirm(Type type, string methodName, object confirmationObject)
    {
        ConfirmationEntries.Add($"{type.FullName}:{methodName}", confirmationObject);
    }

    public static bool TryGetConfirmationMessage(Type type, string methodName, out object confirmationObject)
    {
        return ConfirmationEntries.TryGetValue($"{type.FullName}:{methodName}", out confirmationObject);
    }

    public static bool IsDetourConfirmed(Type type, string methodName, Func<object, bool> compareWithResultPredicate)
    {
        if (ConfirmationEntries.TryGetValue($"{type.FullName}:{methodName}", out var confirmationObject))
        {
            return compareWithResultPredicate(confirmationObject);
        }

        return false;
    }

    public static void Clear()
    {
        ConfirmationEntries.Clear();
    }
}
