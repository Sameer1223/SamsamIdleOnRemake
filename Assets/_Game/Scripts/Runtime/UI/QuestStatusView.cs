using SamsamIdleOn.Core;
using TMPro;
using UnityEngine;

namespace SamsamIdleOn.UI
{
    [DisallowMultipleComponent]
    public sealed class QuestStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text questLabel;
        [SerializeField, TextArea] private string incompleteText = "The Only Quest:\n\nDefeat the Final Boss";
        [SerializeField, TextArea] private string completeText = "The Only Quest:\n\n<s>Defeat the Final Boss</s>";
        [SerializeField] private Color incompleteColor = Color.black;
        [SerializeField] private Color completeColor = new(0.1f, 0.55f, 0.18f, 1f);

        private GameManager gameManager;

        private void Awake()
        {
            if (questLabel == null)
            {
                questLabel = GetComponent<TMP_Text>();
            }
        }

        private void OnEnable()
        {
            ResolveGameManager();

            if (gameManager != null)
            {
                gameManager.StateChanged += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= Refresh;
            }
        }

        private void Refresh()
        {
            if (questLabel == null)
            {
                return;
            }

            ResolveGameManager();
            bool isComplete = gameManager != null && gameManager.IsFinalBossDefeated();
            questLabel.richText = true;
            questLabel.text = isComplete ? completeText : incompleteText;
            questLabel.color = isComplete ? completeColor : incompleteColor;
        }

        private void ResolveGameManager()
        {
            if (gameManager != null)
            {
                return;
            }

            gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();
        }
    }
}
