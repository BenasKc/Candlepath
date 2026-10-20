using UnityEngine;

public class MapSelectMenu : MonoBehaviour
{
    [SerializeField] private MapCatalog catalog;
    [SerializeField] private MapCard cardPrefab;
    [SerializeField] private Transform cardContainer;

    void Start()
    {
        foreach (var map in catalog.Maps)
            Instantiate(cardPrefab, cardContainer).Setup(map);
        
    }
}
