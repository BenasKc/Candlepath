using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Candlepath/Map Catalog")]
public class MapCatalog : ScriptableObject
{
    [SerializeField] private List<MapDefinition> maps = new List<MapDefinition>();

    public IReadOnlyList<MapDefinition> Maps => maps;
}
