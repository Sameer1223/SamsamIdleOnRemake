using System.Collections.Generic;
using UnityEngine;

namespace SamsamIdleOn.Data
{
    [CreateAssetMenu(menuName = "Samsam IdleOn/Data/Game Data Registry", fileName = "GameDataRegistry")]
    public sealed class GameDataRegistry : ScriptableObject
    {
        [SerializeField] private List<Definition> definitions = new();

        private readonly Dictionary<string, Definition> definitionsById = new();

        public bool TryGetDefinition<TDefinition>(string id, out TDefinition definition)
            where TDefinition : Definition
        {
            BuildLookupIfNeeded();

            if (definitionsById.TryGetValue(id, out Definition found) && found is TDefinition typedDefinition)
            {
                definition = typedDefinition;
                return true;
            }

            definition = null;
            return false;
        }

        private void BuildLookupIfNeeded()
        {
            if (definitionsById.Count == definitions.Count)
            {
                return;
            }

            definitionsById.Clear();

            foreach (Definition definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                definitionsById[definition.Id] = definition;
            }
        }
    }
}

