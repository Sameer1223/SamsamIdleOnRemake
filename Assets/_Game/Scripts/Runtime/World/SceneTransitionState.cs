namespace SamsamIdleOn.World
{
    public static class SceneTransitionState
    {
        private static bool hasPendingSourceScene;
        private static int pendingSourceBuildIndex;
        private static string pendingSourceSceneName;

        public static void SetPendingSourceScene(int sourceBuildIndex, string sourceSceneName)
        {
            hasPendingSourceScene = true;
            pendingSourceBuildIndex = sourceBuildIndex;
            pendingSourceSceneName = string.IsNullOrWhiteSpace(sourceSceneName) ? string.Empty : sourceSceneName;
        }

        public static bool TryConsumePendingSourceScene(out int sourceBuildIndex, out string sourceSceneName)
        {
            if (!hasPendingSourceScene)
            {
                sourceBuildIndex = -1;
                sourceSceneName = string.Empty;
                return false;
            }

            sourceBuildIndex = pendingSourceBuildIndex;
            sourceSceneName = pendingSourceSceneName;
            hasPendingSourceScene = false;
            pendingSourceBuildIndex = -1;
            pendingSourceSceneName = string.Empty;
            return true;
        }
    }
}
