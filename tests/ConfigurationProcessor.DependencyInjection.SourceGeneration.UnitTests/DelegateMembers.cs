namespace ConfigurationProcessor.SourceGeneration.UnitTests;

public class DelegateMembers
{
    public void NonStaticTestDelegate()
    {
    }

    public static void TestDelegate()
    {
    }

    public static void TestDelegateOverload(string value)
    {
    }

    public static void TestDelegateOverload(int value)
    {
    }

    public static bool TestDelegateOverload(string svalue = null, int ivalue = 0)
    {
        return true;
    }
}