using SamsamIdleOn.Characters;
using UnityEngine;

namespace SamsamIdleOn.World
{
    [DisallowMultipleComponent]
    public sealed class PlayerSceneSpawn2D : MonoBehaviour
    {
        [SerializeField] private bool stopMovementAfterSpawn = true;

        private void Start()
        {
            if (!SceneTransitionState.TryConsumePendingSourceScene(out int sourceBuildIndex, out string sourceSceneName))
            {
                return;
            }

            if (!TryFindReturnPortal(sourceBuildIndex, sourceSceneName, out ScenePortal2D returnPortal))
            {
                return;
            }

            if (stopMovementAfterSpawn)
            {
                GetComponent<PlayerClickMovement2D>()?.Stop();
            }

            transform.position = returnPortal.SpawnPosition;
        }

        private static bool TryFindReturnPortal(int sourceBuildIndex, string sourceSceneName, out ScenePortal2D returnPortal)
        {
            foreach (ScenePortal2D candidate in FindObjectsByType<ScenePortal2D>(FindObjectsInactive.Exclude))
            {
                if (candidate != null && candidate.TargetsScene(sourceBuildIndex, sourceSceneName))
                {
                    returnPortal = candidate;
                    return true;
                }
            }

            returnPortal = null;
            return false;
        }
    }
}
