using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public AnimalData currentAnimal; // Assign ScriptableObject in inspector
    public GameObject mainBodyPrefab;
    public GameObject bodyPartPrefab; // UI prefab with Image + UIDragSnap2D
    public Transform canvasParent;

    public GameObject mainBody;
    public List<GameObject> spawnedParts = new List<GameObject>();

    void Start()
    {
        SetupAnimal();
    }

    void SetupAnimal()
    {
        mainBody.GetComponent<UnityEngine.UI.Image>().sprite = currentAnimal.mainBody;

        // 2. Create Body Parts
        for (int i = 0; i < currentAnimal.animalBodyPartsSprites.Length; i++)
        {
            GameObject part = Instantiate(bodyPartPrefab, canvasParent);
            part.GetComponent<UnityEngine.UI.Image>().sprite = currentAnimal.animalBodyPartsSprites[i];
            part.GetComponent<RectTransform>().sizeDelta = currentAnimal.size[i];
            // Random scatter position
            Vector2 randomPos = new Vector2(Random.Range(-400, 400), Random.Range(-200, 200));
            part.GetComponent<RectTransform>().anchoredPosition = randomPos;

            // Store correct snap position
            UIDragSnap2D dragComp = part.GetComponent<UIDragSnap2D>();
            dragComp.correctPosition = currentAnimal.bodypartsTransform[i];
            dragComp.mainBody = mainBody;
            dragComp.startPosition = part.GetComponent<RectTransform>().anchoredPosition;

            spawnedParts.Add(part);
        }
    }
}
