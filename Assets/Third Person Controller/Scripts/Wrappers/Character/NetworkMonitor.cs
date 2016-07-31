#if !(UNITY_4_6 || UNITY_4_7 || UNITY_5_0)
namespace Opsive.ThirdPersonController.Wrappers
{
    /// <summary>
    /// Wrapper component to prevent the references from being lost when switching from the Third Person Controller assembly to the Third Person Controller source.
    /// See this page for information on importing the source code: http://opsive.com/assets/ThirdPersonController/documentation.php?id=50.
    /// </summary>
    public class NetworkMonitor : Opsive.ThirdPersonController.NetworkMonitor
    {
        // Intentionally left blank. The parent class has all of the implementation.
    }
}
#endif